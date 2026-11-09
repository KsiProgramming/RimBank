using RimBank.Host;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.16: party_reference_data_directory_retrieve sends the same query as the HTTP endpoint and answers only from
// the entry's inline view (specs/tech-stack.md §6, §8, ADR 0039).
public sealed class RetrievePartyReferenceDataDirectoryEntryToolTests(PostgresqlServer server)
{
    [Fact]
    public async Task Retrieve_answers_the_registered_entry_with_its_version()
    {
        await using var host = await server.StartHostAsync();
        var registered = (await RegisterPartyToolTests.RegisterAsync(host, "Organisation", "retrieve-1")).Success();
        var id = registered.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid();

        var entry = (await RetrieveAsync(host, id.ToString())).Success();

        entry.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ShouldBe(id);
        entry.GetProperty("PartyType").GetString().ShouldBe("Organisation");
        entry.GetProperty("DirectoryEntryDateType").GetString().ShouldBe("OpenDate");
        entry.GetProperty("DirectoryEntryDate").GetDateTimeOffset().ShouldBe(PostgresqlServer.Now);
        foreach (var name in new[] { "DemographicsId", "ReferenceId", "BankRelationsId" })
        {
            entry.GetProperty(name).GetGuid().ShouldBe(registered.GetProperty(name).GetGuid());
        }

        entry.TryGetProperty("RefreshDate", out _).ShouldBeFalse();
        entry.GetProperty("version").GetInt64().ShouldBe(registered.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task An_unknown_entry_is_a_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        (await RetrieveAsync(host, Guid.CreateVersion7().ToString())).Problem(404, PartyReferenceDataDirectoryEntryErrors.PartyNotFound.Code);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_malformed_id_is_a_validation_problem(string id)
    {
        await using var host = await server.StartHostAsync();

        (await RetrieveAsync(host, id)).Problem(400, PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid.Code);
    }

    [Fact]
    public async Task A_caller_with_only_the_write_scope_neither_sees_nor_retrieves()
    {
        await using var host = await server.StartHostAsync();
        var registered = (await RegisterPartyToolTests.RegisterAsync(host, "Person", "retrieve-denied")).Success();
        var client = await host.ConnectAsync([PartyReferenceDataDirectoryModule.WriteScope]);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await host.CallAsync(
            RetrievePartyReferenceDataDirectoryEntryTool.Name,
            new() { ["partyReferenceDataDirectoryEntryId"] = registered.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ToString() },
            [PartyReferenceDataDirectoryModule.WriteScope]);

        tools.Select(tool => tool.Name).ShouldNotContain(RetrievePartyReferenceDataDirectoryEntryTool.Name);
        result.Problem(403, "shared.forbidden");
    }

    internal static Task<ModelContextProtocol.Protocol.CallToolResult> RetrieveAsync(PartyMcpHost host, string id) =>
        host.CallAsync(RetrievePartyReferenceDataDirectoryEntryTool.Name, new() { ["partyReferenceDataDirectoryEntryId"] = id });
}
