using Alba;
using Microsoft.Net.Http.Headers;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.AssociationScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.11: BIAN Associations/Retrieve over HTTP. A query answered from the association's view with its version as
// ETag; every error is Problem Details (specs/tech-stack.md §5, ADR 0024).
public sealed class RetrievePartyAssociationEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Retrieve_shows_what_Register_and_Update_recorded_with_the_association_version()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);
        var updated = await PutAssociationOkAsync(
            host,
            AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"),
            """{ "AssociateType": "PartyIsMarriedToParty" }""",
            "\"1\"",
            "\"2\"");

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Retrieve"));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        (await result.ReadAsJsonAsync<PartyAssociationResponse>()).ShouldBe(updated);
    }

    [Fact]
    public async Task A_current_if_none_match_is_304()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);

        await host.Alba.Scenario(_ =>
        {
            Get(_, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Retrieve"), ifNoneMatch: "\"1\"");
            _.StatusCodeShouldBe(304);
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });
    }

    [Fact]
    public async Task Only_the_owner_finds_the_association()
    {
        await using var host = await server.StartHostAsync();
        var (_, associate, association) = await AssociateAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, AssociationUrl(associate.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Retrieve"));
            _.StatusCodeShouldBe(404);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await ProblemOf(result, 404)).GetProperty("errorCode").GetString().ShouldBe("party.association-not-found");
    }

    [Fact]
    public async Task An_association_id_that_is_not_an_identifier_is_a_problem()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, AssociationUrl(Guid.CreateVersion7().ToString(), "12", "Retrieve"));
            _.StatusCodeShouldBe(400);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await ProblemOf(result, 400)).GetProperty("errors").TryGetProperty("associationsid", out _).ShouldBeTrue();
    }
}
