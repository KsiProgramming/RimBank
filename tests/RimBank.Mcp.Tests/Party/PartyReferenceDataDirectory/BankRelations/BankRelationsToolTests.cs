using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the BankRelations tools send the same Update and Retrieve as the HTTP endpoints, with expectedVersion in
// place of If-Match and the BIAN body as their request argument (ADR 0035, ADR 0039, ADR 0040).
public sealed class BankRelationsToolTests(PostgresqlServer server)
{
    [Fact]
    public async Task A_relationship_manager_is_recorded_and_retrieved()
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);
        var manager = await Entry.RegisterAsync(host, "Person");

        var updated = (await UpdateAsync(host, customer, Managed(manager.Id, "RelationshipManager"), customer.Version, PartyTools.Key("update"))).Success();
        var retrieved = (await RetrieveAsync(host, customer.Id.ToString(), customer.BankRelationsId.ToString())).Success();

        updated.GetProperty("BankRelationType").GetString().ShouldBe("RelationshipManager");
        updated.GetProperty("BusinessUnitEmployeeReference").GetProperty("PartyReference").GetGuid().ShouldBe(manager.Id);
        updated.GetProperty("BankRelationReference").GetProperty("RelationshipLifecycleStatus").GetProperty("StatusDescription").GetString()
            .ShouldBe("Active");
        updated.GetProperty("version").GetInt64().ShouldBe(customer.Version + 1);
        JsonElement.DeepEquals(retrieved, updated).ShouldBeTrue($"{retrieved} vs {updated}");
    }

    [Fact]
    public async Task A_servicing_branch_names_an_organisation()
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);
        var branch = await Entry.RegisterAsync(host, "Organisation");

        var updated = (await UpdateAsync(host, customer, Managed(branch.Id, "ServicingBranch"), customer.Version, PartyTools.Key("branch"))).Success();

        updated.GetProperty("BankRelationType").GetString().ShouldBe("ServicingBranch");
    }

    [Fact]
    public async Task A_person_as_servicing_branch_or_an_unregistered_party_is_refused_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);
        var person = await Entry.RegisterAsync(host, "Person");

        (await UpdateAsync(host, customer, Managed(person.Id, "ServicingBranch"), customer.Version, PartyTools.Key("mismatch")))
            .Problem(422, "party.referenced-party-type-mismatch");
        (await UpdateAsync(host, customer, Managed(Guid.CreateVersion7(), "RelationshipManager"), customer.Version, PartyTools.Key("unknown")))
            .Problem(422, "party.referenced-party-not-registered");

        (await host.ScalarAsync<long>(PartyTools.EventsOf(customer.Id))).ShouldBe(1);
    }

    [Fact]
    public async Task A_part_rimbank_does_not_record_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);

        (await UpdateAsync(host, customer, """{ "BankRelationReference": { "RelationshipType": "Primary" } }""", customer.Version, PartyTools.Key("refused")))
            .Problem(400, "party.bank-relations-field-not-supported");
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);
        (await UpdateAsync(host, customer, "{}", customer.Version, PartyTools.Key("first"))).Success();

        (await UpdateAsync(host, customer, "{}", customer.Version, PartyTools.Key("stale")))
            .Problem(409, PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code);
    }

    internal static string Managed(Guid party, string type) =>
        $$"""
        {
          "BankRelationType": "{{type}}",
          "BusinessUnitEmployeeReference": { "PartyReference": "{{party}}" },
          "BankRelationReference": {
            "RelationshipStartDate": { "DateTimeContent": "2024-01-01" },
            "RelationshipLifecycleStatus": { "StatusDescription": "Active" }
          }
        }
        """;

    internal static Task<CallToolResult> UpdateAsync(PartyMcpHost host, Entry entry, string request, long expectedVersion, string key) =>
        host.CallAsync(
            UpdateBankRelationsTool.Name,
            new()
            {
                ["partyReferenceDataDirectoryEntryId"] = entry.Id.ToString(),
                ["bankRelationsId"] = entry.BankRelationsId.ToString(),
                ["request"] = JsonElement.Parse(request),
                ["expectedVersion"] = expectedVersion,
                ["idempotencyKey"] = key,
            });

    internal static Task<CallToolResult> RetrieveAsync(PartyMcpHost host, string entryId, string bankRelationsId) =>
        host.CallAsync(
            RetrieveBankRelationsTool.Name,
            new() { ["partyReferenceDataDirectoryEntryId"] = entryId, ["bankRelationsId"] = bankRelationsId });
}
