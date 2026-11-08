using System.Text.Json;
using Alba;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.AssociationScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.11: BIAN Associations/Register over HTTP. A command with an Idempotency-Key, answered from its acknowledgement
// with the association's version as ETag; every error is Problem Details (specs/tech-stack.md §5, ADR 0024, ADR 0033).
public sealed class RegisterPartyAssociationEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Register_associates_two_parties_and_answers_the_association()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);
        var associate = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            Post(_, RegisterUrl(owner.PartyReferenceDataDirectoryEntryId), RegisterBody(associate.PartyReferenceDataDirectoryEntryId));
            _.StatusCodeShouldBeOk();
        });

        var body = await result.ReadAsJsonAsync<JsonElement>();
        Guid.Parse(body.GetProperty("AssociationsId").GetString()!).Version.ShouldBe(7);
        body.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ShouldBe(owner.PartyReferenceDataDirectoryEntryId);
        body.GetProperty("AssociateReference").GetProperty("PartyReference").GetString()
            .ShouldBe(associate.PartyReferenceDataDirectoryEntryId.ToString());
        body.GetProperty("AssociateType").GetString().ShouldBe("PartyIsParentOfParty");
        body.GetProperty("AssociationValidFromToDate").GetProperty("FromDateTime").GetString().ShouldBe("2024-01-01T00:00:00Z");
        body.GetProperty("AssociationValidFromToDate").TryGetProperty("ToDateTime", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_idempotency_key_is_a_problem()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);
        var associate = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            Post(_, RegisterUrl(owner.PartyReferenceDataDirectoryEntryId), RegisterBody(associate.PartyReferenceDataDirectoryEntryId), key: "");
            _.StatusCodeShouldBe(400);
            _.ContentTypeShouldBe(ProblemJson);
        });

        await ProblemOf(result, 400);
    }

    [Fact]
    public async Task An_owner_id_that_is_not_an_identifier_is_a_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await PostProblemAsync(host, RegisterUrl("not-an-id"), RegisterBody(Guid.CreateVersion7()), 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").TryGetProperty("partyreferencedatadirectoryid", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Invalid_values_are_named_with_their_bian_paths()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);

        var problem = await PostProblemAsync(
            host,
            RegisterUrl(owner.PartyReferenceDataDirectoryEntryId),
            """{ "AssociateType": "Cousin", "AssociationValidFromToDate": { "FromDateTime": "2025-01-01", "ToDateTime": "2024-01-01" } }""",
            400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.association-invalid");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("AssociateReference.PartyReference", out _).ShouldBeTrue();
        errors.TryGetProperty("AssociateType", out _).ShouldBeTrue();
        errors.TryGetProperty("AssociationValidFromToDate.ToDateTime", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Fields_rimbank_does_not_record_are_refused()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);

        var problem = await PostProblemAsync(
            host,
            RegisterUrl(owner.PartyReferenceDataDirectoryEntryId),
            """
            {
              "AssociateReference": { "PartyReference": "x", "InvolvementReference": "Guarantor" },
              "AssociateType": "PartyIsParentOfParty",
              "productinstancereference": { "ProductInstanceReference": "P-1" },
              "PreferredBeneficiary": null
            }
            """,
            400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.association-field-not-supported");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name)
            .ShouldBe(["ProductInstanceReference", "AssociateReference.InvolvementReference"]);
    }

    [Fact]
    public async Task An_unknown_owner_is_not_found()
    {
        await using var host = await server.StartHostAsync();
        var associate = await RegisterAsync(host);

        var problem = await PostProblemAsync(
            host, RegisterUrl(Guid.CreateVersion7()), RegisterBody(associate.PartyReferenceDataDirectoryEntryId), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task A_party_cannot_be_associated_with_itself()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);

        var problem = await PostProblemAsync(
            host, RegisterUrl(owner.PartyReferenceDataDirectoryEntryId), RegisterBody(owner.PartyReferenceDataDirectoryEntryId), 422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.self-association");
    }

    [Fact]
    public async Task An_associate_that_is_not_registered_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var owner = await RegisterAsync(host);

        var problem = await PostProblemAsync(
            host, RegisterUrl(owner.PartyReferenceDataDirectoryEntryId), RegisterBody(Guid.CreateVersion7()), 422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.associate-not-registered");
    }
}
