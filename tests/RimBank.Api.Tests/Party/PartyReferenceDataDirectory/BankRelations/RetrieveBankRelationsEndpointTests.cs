using Alba;
using Microsoft.Net.Http.Headers;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.BankRelationsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.12: GET …/BankRelations/{bankrelationsid}/Retrieve over HTTP, answered from the entry's inline view with its
// version as ETag (specs/tech-stack.md §5, ADR 0024, ADR 0035).
public sealed class RetrieveBankRelationsEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Retrieve_shows_what_update_recorded_with_the_entry_version()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        var manager = await RegisterAsync(host);
        await PutBankRelationsOkAsync(host, BankRelationsUpdateUrl(customer), ManagedBody(manager.PartyReferenceDataDirectoryEntryId), "\"1\"", "\"2\"");

        var result = await host.Alba.Scenario(_ =>
        {
            AssociationScenarios.Get(_, BankRelationsRetrieveUrl(customer));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        (await result.ReadAsJsonAsync<BankRelationsResponse>()).ShouldBe(ManagedResponse(customer, manager.PartyReferenceDataDirectoryEntryId));
    }

    [Fact]
    public async Task Bank_relations_never_updated_answer_the_ids_only()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            AssociationScenarios.Get(_, BankRelationsRetrieveUrl(customer));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });

        (await result.ReadAsTextAsync()).ShouldBe(
            $$"""{"BankRelationsId":"{{customer.BankRelationsId}}","PartyReferenceDataDirectoryEntryId":"{{customer.PartyReferenceDataDirectoryEntryId}}"}""");
    }

    [Fact]
    public async Task A_current_If_None_Match_is_not_modified()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        await host.Alba.Scenario(_ =>
        {
            AssociationScenarios.Get(_, BankRelationsRetrieveUrl(customer), ifNoneMatch: "\"1\"");
            _.StatusCodeShouldBe(304);
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });
    }

    [Fact]
    public async Task An_unknown_entry_is_a_party_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await GetBankRelationsProblemAsync(host, BankRelationsUrl(Guid.CreateVersion7(), Guid.CreateVersion7(), "Retrieve"), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task A_foreign_bank_relations_id_is_a_behavior_qualifier_not_found_problem()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var problem = await GetBankRelationsProblemAsync(
            host, BankRelationsUrl(customer.PartyReferenceDataDirectoryEntryId, other.BankRelationsId, "Retrieve"), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
    }

    [Fact]
    public async Task A_malformed_bank_relations_id_is_a_400_naming_the_parameter()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var problem = await GetBankRelationsProblemAsync(
            host, BankRelationsUrl(customer.PartyReferenceDataDirectoryEntryId.ToString(), Guid.NewGuid().ToString(), "Retrieve"), 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        ErrorFields(problem).ShouldBe(["bankrelationsid"]);
    }
}
