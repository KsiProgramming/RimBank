using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.16: the same operation answers the same on HTTP and MCP. The tool's result mirrors the HTTP body, with the
// ETag as its version, and an error is the same Problem Details (mission principles 6 and 8, tech-stack §6, ADR 0039).
public sealed class PartyMcpParityTests(PostgresqlServer server)
{
    private const string Register = "/PartyReferenceDataDirectory/Register";

    [Fact]
    public async Task Register_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();

        var http = await host.Alba.Scenario(_ => PostRegister(_, """{ "PartyType": "Person" }""", "parity-http"));
        var tool = (await RegisterPartyToolTests.RegisterAsync(host, "Person", "parity-mcp")).Success();

        var body = await http.ReadAsJsonAsync<JsonElement>();
        Members(tool).ShouldBe([.. Members(body), "version"]);
        foreach (var member in new[] { "PartyType", "DirectoryEntryDateType", "DirectoryEntryDate" })
        {
            JsonElement.DeepEquals(tool.GetProperty(member), body.GetProperty(member)).ShouldBeTrue(member);
        }

        Parity.ETagOf(http).ShouldBe(tool.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Retrieve_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var id = (await RegisterPartyToolTests.RegisterAsync(host, "Organisation", "parity-retrieve")).Success()
            .GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid();

        var http = await host.Alba.Scenario(_ => GetRetrieve(_, id.ToString(), 200));
        var tool = (await RetrievePartyReferenceDataDirectoryEntryToolTests.RetrieveAsync(host, id.ToString())).Success();

        var body = await http.ReadAsJsonAsync<JsonElement>();
        JsonElement.DeepEquals(Parity.Without(tool, "version"), body).ShouldBeTrue($"{tool} vs {body}");
        Parity.ETagOf(http).ShouldBe(tool.GetProperty("version").GetInt64());
    }

    [Theory]
    [InlineData("Alien")]
    [InlineData("")]
    public async Task An_invalid_party_type_is_the_same_problem(string partyType)
    {
        await using var host = await server.StartHostAsync();

        var http = await host.Alba.Scenario(_ =>
        {
            PostRegister(_, JsonSerializer.Serialize(new { PartyType = partyType }), "parity-invalid-http");
            _.StatusCodeShouldBe(400);
        });
        var tool = await RegisterPartyToolTests.RegisterAsync(host, partyType, "parity-invalid-mcp");

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task A_reused_idempotency_key_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        await host.Alba.Scenario(_ => PostRegister(_, """{ "PartyType": "Person" }""", "parity-reused"));
        (await RegisterPartyToolTests.RegisterAsync(host, "Person", "parity-reused-mcp")).Success();

        var http = await host.Alba.Scenario(_ =>
        {
            PostRegister(_, """{ "PartyType": "Organisation" }""", "parity-reused");
            _.StatusCodeShouldBe(409);
        });
        var tool = await RegisterPartyToolTests.RegisterAsync(host, "Organisation", "parity-reused-mcp");

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Theory]
    [InlineData("not-a-guid", 400)]
    [InlineData("01a1b031-8dc0-74cc-a63a-f954f0f3f1f4", 404)]
    public async Task A_malformed_or_unknown_id_is_the_same_problem(string id, int status)
    {
        await using var host = await server.StartHostAsync();

        var http = await host.Alba.Scenario(_ => GetRetrieve(_, id, status));
        var tool = await RetrievePartyReferenceDataDirectoryEntryToolTests.RetrieveAsync(host, id);

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task A_missing_scope_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var readOnly = TestTokens.Issue(scopes: [PartyReferenceDataDirectoryModule.ReadScope]);

        var http = await host.Alba.Scenario(_ =>
        {
            _.Post.Text("""{ "PartyType": "Person" }""").ToUrl(Register);
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + readOnly);
            _.WithRequestHeader("Idempotency-Key", "parity-forbidden");
            _.StatusCodeShouldBe(403);
        });
        var tool = await host.CallAsync(
            RegisterPartyTool.Name,
            new() { ["partyType"] = "Person", ["idempotencyKey"] = "parity-forbidden" },
            [PartyReferenceDataDirectoryModule.ReadScope]);

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    private static void PostRegister(Scenario scenario, string body, string key)
    {
        scenario.Post.Text(body).ToUrl(Register);
        scenario.WithRequestHeader(HeaderNames.ContentType, "application/json");
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
        scenario.WithRequestHeader("Idempotency-Key", key);
    }

    private static void GetRetrieve(Scenario scenario, string id, int status)
    {
        scenario.Get.Url($"/PartyReferenceDataDirectory/{id}/Retrieve");
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
        scenario.StatusCodeShouldBe(status);
    }

    private static string[] Members(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
