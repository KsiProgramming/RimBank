using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.AssociationScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.11: BIAN Associations/Update over HTTP. A partial update with an Idempotency-Key and If-Match (ADR 0027),
// answered from its acknowledgement; every error is Problem Details (ADR 0024, ADR 0033).
public sealed class UpdatePartyAssociationEndpointTests(PostgresqlServer server)
{
    private const string EndThePeriod =
        """{ "AssociationValidFromToDate": { "FromDateTime": "2024-01-01", "ToDateTime": "2025-12-31T23:30:00+01:00" } }""";

    [Fact]
    public async Task Update_changes_the_period_keeps_the_type_and_answers_the_next_version()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);

        var updated = await PutAssociationOkAsync(
            host, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"), EndThePeriod, "\"1\"", "\"2\"");

        updated.AssociateType.ShouldBe("PartyIsParentOfParty");
        updated.AssociationValidFromToDate.ShouldNotBeNull().ToDateTime.ShouldBe("2025-12-31T22:30:00Z");
        updated.AssociateReference.PartyReference.ShouldBe(association.AssociateReference.PartyReference);
    }

    [Fact]
    public async Task Update_changes_the_type_and_an_empty_period_clears_it()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate, association) = await AssociateAsync(host);

        var updated = await PutAssociationOkAsync(
            host,
            AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"),
            $$"""
            {
              "AssociateReference": { "PartyReference": "{{associate.PartyReferenceDataDirectoryEntryId}}" },
              "AssociateType": "PartyIsTrustedByParty",
              "AssociationValidFromToDate": {}
            }
            """,
            "\"1\"",
            "\"2\"");

        updated.AssociateType.ShouldBe("PartyIsTrustedByParty");
        updated.AssociationValidFromToDate.ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_if_match_is_428()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);

        var problem = await PutProblemAsync(
            host, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"), EndThePeriod, null, 428);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-required");
    }

    [Fact]
    public async Task A_malformed_if_match_is_400()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);

        var problem = await PutProblemAsync(
            host, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"), EndThePeriod, "*", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-invalid");
    }

    [Fact]
    public async Task A_stale_if_match_is_a_version_conflict()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);
        var url = AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update");
        await PutAssociationOkAsync(host, url, EndThePeriod, "\"1\"", "\"2\"");

        var problem = await PutProblemAsync(host, url, EndThePeriod, "\"1\"", 409);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.version-conflict");
    }

    [Fact]
    public async Task An_association_id_that_is_not_an_identifier_is_a_problem()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId.ToString(), "nope", "Update"), EndThePeriod, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").TryGetProperty("associationsid", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task An_entry_that_does_not_own_the_association_does_not_find_it()
    {
        await using var host = await server.StartHostAsync();
        var (_, associate, association) = await AssociateAsync(host);

        var problem = await PutProblemAsync(
            host, AssociationUrl(associate.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"), EndThePeriod, "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.association-not-found");
    }

    [Fact]
    public async Task An_unknown_association_is_not_found()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, Guid.CreateVersion7(), "Update"), EndThePeriod, "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.association-not-found");
    }

    [Fact]
    public async Task Another_associate_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);
        var other = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host,
            AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"),
            $$"""{ "AssociateReference": { "PartyReference": "{{other.PartyReferenceDataDirectoryEntryId}}" } }""",
            "\"1\"",
            422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.associate-cannot-change");
    }

    [Fact]
    public async Task The_type_cannot_be_cleared()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociateAsync(host);

        var problem = await PutProblemAsync(
            host, AssociationUrl(owner.PartyReferenceDataDirectoryEntryId, association.AssociationsId, "Update"), """{ "AssociateType": "" }""", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.association-invalid");
        problem.GetProperty("errors").TryGetProperty("AssociateType", out _).ShouldBeTrue();
    }
}
