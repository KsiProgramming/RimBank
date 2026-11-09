using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: party_reference_data_directory_update sends the same UpdatePartyReferenceDataDirectoryEntry as the HTTP
// endpoint, with expectedVersion in place of If-Match and the BIAN body as its request argument (ADR 0039, ADR 0040).
public sealed class UpdatePartyReferenceDataDirectoryEntryToolTests(PostgresqlServer server)
{
    [Fact]
    public async Task The_request_argument_is_the_bian_body_with_its_property_names()
    {
        await using var host = await server.StartHostAsync();
        var tool = host.Alba.Services.GetServices<McpServerTool>().Single(tool => tool.ProtocolTool.Name == UpdatePartyReferenceDataDirectoryEntryTool.Name);
        var schema = tool.ProtocolTool.InputSchema;
        TestContext.Current.TestOutputHelper?.WriteLine(schema.GetRawText());

        schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ShouldBe(
            ["partyReferenceDataDirectoryEntryId", "request", "expectedVersion", "idempotencyKey"],
            ignoreOrder: true);
        var request = schema.GetProperty("properties").GetProperty("request");
        request.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["PartyReference", "DirectoryEntryDateType", "DirectoryEntryDate", "PartyType"], ignoreOrder: true);
        schema.GetProperty("properties").GetProperty("expectedVersion").GetProperty("type").GetString().ShouldBe("integer");
    }

    [Fact]
    public async Task Update_answers_the_entry_with_its_new_version_and_refresh_date()
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");

        var entry = (await UpdateAsync(host, id, """{ "PartyType": "Organisation" }""", version, "update-1")).Success();

        entry.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ShouldBe(id);
        entry.GetProperty("PartyType").GetString().ShouldBe("Organisation");
        entry.GetProperty("RefreshDate").GetDateTimeOffset().ShouldBe(PostgresqlServer.Now);
        entry.GetProperty("version").GetInt64().ShouldBe(version + 1);
    }

    [Fact]
    public async Task A_left_out_party_type_keeps_the_current_one_and_ignored_fields_are_accepted()
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");

        var entry = (await UpdateAsync(host, id, """{ "PartyReference": { "x": 1 }, "Unknown": true }""", version, "update-keep")).Success();

        entry.GetProperty("PartyType").GetString().ShouldBe("Person");
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");
        (await UpdateAsync(host, id, "{}", version, "update-first")).Success();

        (await UpdateAsync(host, id, "{}", version, "update-stale")).Problem(409, PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code);
    }

    [Fact]
    public async Task The_same_key_and_arguments_replay_the_first_answer()
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");

        var first = (await UpdateAsync(host, id, """{ "PartyType": "Organisation" }""", version, "update-twice")).Success();
        var second = (await UpdateAsync(host, id, """{ "PartyType": "Organisation" }""", version, "update-twice")).Success();

        second.GetRawText().ShouldBe(first.GetRawText());
        (await host.ScalarAsync<long>(PartyTools.EventsOf(id))).ShouldBe(2);
    }

    [Theory]
    [InlineData("", "party.party-type-invalid")]
    [InlineData("Alien", "party.party-type-invalid")]
    public async Task An_invalid_party_type_is_a_validation_problem(string partyType, string errorCode)
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");

        (await UpdateAsync(host, id, JsonSerializer.Serialize(new { PartyType = partyType }), version, "update-invalid"))
            .Problem(400, errorCode);
    }

    [Fact]
    public async Task An_unknown_entry_is_a_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        (await UpdateAsync(host, Guid.CreateVersion7(), "{}", 1, "update-unknown"))
            .Problem(404, PartyReferenceDataDirectoryEntryErrors.PartyNotFound.Code);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("expectedVersion")]
    [InlineData("idempotencyKey")]
    public async Task A_missing_argument_is_a_tool_arguments_problem(string missing)
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");
        var arguments = Arguments(id, "{}", version, "update-missing");
        arguments.Remove(missing);

        (await host.CallAsync(UpdatePartyReferenceDataDirectoryEntryTool.Name, arguments)).Problem(400, "shared.tool-arguments-invalid");
        (await host.ScalarAsync<long>(PartyTools.EventsOf(id))).ShouldBe(1);
    }

    [Fact]
    public async Task A_request_that_is_not_an_object_is_a_tool_arguments_problem()
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");
        var arguments = Arguments(id, "{}", version, "update-not-object");
        arguments["request"] = "Organisation";

        (await host.CallAsync(UpdatePartyReferenceDataDirectoryEntryTool.Name, arguments)).Problem(400, "shared.tool-arguments-invalid");
    }

    internal static Task<CallToolResult> UpdateAsync(PartyMcpHost host, Guid id, string request, long expectedVersion, string key) =>
        host.CallAsync(UpdatePartyReferenceDataDirectoryEntryTool.Name, Arguments(id, request, expectedVersion, key));

    private static Dictionary<string, object?> Arguments(Guid id, string request, long expectedVersion, string key) =>
        new()
        {
            ["partyReferenceDataDirectoryEntryId"] = id.ToString(),
            ["request"] = JsonElement.Parse(request),
            ["expectedVersion"] = expectedVersion,
            ["idempotencyKey"] = key,
        };
}
