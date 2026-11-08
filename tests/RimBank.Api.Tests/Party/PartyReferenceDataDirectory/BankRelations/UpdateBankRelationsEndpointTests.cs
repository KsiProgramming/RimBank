using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.BankRelationsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.12: PUT …/BankRelations/{bankrelationsid}/Update over HTTP (specs/tech-stack.md §5, ADR 0027, ADR 0035). The
// employee or unit is checked on the write side; every refusal is Problem Details and stores nothing.
public sealed class UpdateBankRelationsEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task A_relationship_manager_is_recorded_and_answered_with_the_next_version()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        var manager = await RegisterAsync(host);

        var updated = await PutBankRelationsOkAsync(
            host, BankRelationsUpdateUrl(customer), ManagedBody(manager.PartyReferenceDataDirectoryEntryId), "\"1\"", "\"2\"");

        updated.ShouldBe(ManagedResponse(customer, manager.PartyReferenceDataDirectoryEntryId));
    }

    [Fact]
    public async Task A_partial_update_keeps_what_it_does_not_send_and_an_empty_relationship_clears_the_dates_and_status()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        var manager = await RegisterAsync(host);
        await PutBankRelationsOkAsync(host, BankRelationsUpdateUrl(customer), ManagedBody(manager.PartyReferenceDataDirectoryEntryId), "\"1\"", "\"2\"");

        var ended = await PutBankRelationsOkAsync(
            host,
            BankRelationsUpdateUrl(customer),
            """{ "BankRelationReference": { "RelationshipEndDate": { "DateTimeContent": "2026-06-30T02:00:00+02:00" }, "RelationshipLifecycleStatus": { "StatusDescription": "Ended" } } }""",
            "\"2\"",
            "\"3\"");
        var cleared = await PutBankRelationsOkAsync(host, BankRelationsUpdateUrl(customer), """{ "BankRelationReference": {} }""", "\"3\"", "\"4\"");

        ended.ShouldBe(ManagedResponse(customer, manager.PartyReferenceDataDirectoryEntryId) with
        {
            BankRelationReference = new BankRelationReferenceModel
            {
                RelationshipStartDate = new DateTimeModel { DateTimeContent = "2024-01-01T00:00:00Z" },
                RelationshipEndDate = new DateTimeModel { DateTimeContent = "2026-06-30T00:00:00Z" },
                RelationshipLifecycleStatus = new RelationshipLifecycleStatusModel { StatusDescription = "Ended" },
            },
        });
        cleared.ShouldBe(ManagedResponse(customer, manager.PartyReferenceDataDirectoryEntryId) with { BankRelationReference = null });
    }

    [Fact]
    public async Task A_branch_services_an_organisation()
    {
        await using var host = await server.StartHostAsync();
        var company = await RegisterAsync(host, "Organisation");
        var branch = await RegisterAsync(host, "Organisation");

        var updated = await PutBankRelationsOkAsync(
            host,
            BankRelationsUpdateUrl(company),
            $$"""{ "BankRelationType": "ServicingBranch", "BusinessUnitEmployeeReference": { "PartyReference": "{{branch.PartyReferenceDataDirectoryEntryId}}" } }""",
            "\"1\"",
            "\"2\"");

        updated.ShouldBe(new BankRelationsResponse(
            company.BankRelationsId,
            company.PartyReferenceDataDirectoryEntryId,
            "ServicingBranch",
            new InvolvedPartyModel { PartyReference = branch.PartyReferenceDataDirectoryEntryId.ToString() }));
    }

    [Fact]
    public async Task A_person_as_servicing_branch_is_a_422_type_mismatch_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        var person = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host, BankRelationsUpdateUrl(customer), ManagedBody(person.PartyReferenceDataDirectoryEntryId, "ServicingBranch"), "\"1\"", 422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.referenced-party-type-mismatch");
        problem.GetProperty("detail").GetString().ShouldBe("BusinessUnitEmployeeReference.PartyReference must name a party of type Organisation.");
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{customer.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task The_party_itself_as_its_relationship_manager_is_a_422_self_reference()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, BankRelationsUpdateUrl(customer), ManagedBody(customer.PartyReferenceDataDirectoryEntryId), "\"1\"", 422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.self-reference");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldStartWith("BusinessUnitEmployeeReference.PartyReference");
    }

    [Fact]
    public async Task An_employee_that_is_not_registered_is_a_422_and_an_association_id_is_not_an_entry()
    {
        await using var host = await server.StartHostAsync();
        var (owner, _, association) = await AssociationScenarios.AssociateAsync(host);

        var unknown = await PutProblemAsync(host, BankRelationsUpdateUrl(owner), ManagedBody(Guid.CreateVersion7()), "\"1\"", 422);
        var associationId = await PutProblemAsync(host, BankRelationsUpdateUrl(owner), ManagedBody(association.AssociationsId), "\"1\"", 422);

        unknown.GetProperty("errorCode").GetString().ShouldBe("party.referenced-party-not-registered");
        associationId.GetProperty("errorCode").GetString().ShouldBe("party.referenced-party-not-registered");
    }

    [Fact]
    public async Task Every_invalid_value_is_a_400_naming_its_path()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host,
            BankRelationsUpdateUrl(customer),
            """
            {
              "BankRelationType": "Customer",
              "BusinessUnitEmployeeReference": { "PartyReference": "EMP-1" },
              "BankRelationReference": {
                "RelationshipStartDate": { "DateTimeContent": "01/01/2024" },
                "RelationshipEndDate": { "DateTimeContent": "soon" },
                "RelationshipLifecycleStatus": { "StatusDescription": "Closed" }
              }
            }
            """,
            "\"1\"",
            400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.bank-relations-invalid");
        ErrorFields(problem).ShouldBe(
        [
            "BankRelationType",
            "BusinessUnitEmployeeReference.PartyReference",
            "BankRelationReference.RelationshipStartDate.DateTimeContent",
            "BankRelationReference.RelationshipEndDate.DateTimeContent",
            "BankRelationReference.RelationshipLifecycleStatus.StatusDescription",
        ],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData("""{ "BusinessUnitEmployeeReference": { "PartyReference": "018f0000-0000-7000-8000-000000000000" } }""", "BankRelationType")]
    [InlineData("""{ "BankRelationReference": { "RelationshipStartDate": { "DateTimeContent": "2025-01-02" }, "RelationshipEndDate": { "DateTimeContent": "2025-01-01" } } }""", "BankRelationReference.RelationshipEndDate.DateTimeContent")]
    public async Task A_resulting_bank_relations_that_breaks_a_value_rule_is_a_400(string body, string field)
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, BankRelationsUpdateUrl(customer), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.bank-relations-invalid");
        ErrorFields(problem).ShouldBe([field]);
    }

    [Fact]
    public async Task Every_part_rimbank_does_not_record_is_refused_by_path_before_the_values_are_read()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host,
            BankRelationsUpdateUrl(customer),
            """
            {
              "BankRelationType": "Customer",
              "BusinessUnitEmployeeReference": { "InvolvementReference": "RM" },
              "BankRelationReference": {
                "RelationshipType": "Primary",
                "RelationshipValidityPeriod": { "FromDateTime": { "DateTimeContent": "2024-01-01" } },
                "RelationshipIdentification": { "IdentifierValue": "REL-1" },
                "RelationshipStartDate": { "DateTimeContent": "2024-01-01", "TimeZoneCode": "CET" },
                "RelationshipLifecycleStatus": { "StatusDescription": "Active", "StatusReason": "Onboarded" },
                "RelationshipEndDate": null
              }
            }
            """,
            "\"1\"",
            400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.bank-relations-field-not-supported");
        ErrorFields(problem).ShouldBe(
        [
            "BusinessUnitEmployeeReference.InvolvementReference",
            "BankRelationReference.RelationshipType",
            "BankRelationReference.RelationshipValidityPeriod",
            "BankRelationReference.RelationshipIdentification",
            "BankRelationReference.RelationshipStartDate.TimeZoneCode",
            "BankRelationReference.RelationshipLifecycleStatus.StatusReason",
        ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_409_and_a_missing_one_a_428()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        await PutBankRelationsOkAsync(host, BankRelationsUpdateUrl(customer), "{}", "\"1\"", "\"2\"");

        var stale = await PutProblemAsync(host, BankRelationsUpdateUrl(customer), "{}", "\"1\"", 409);
        var missing = await PutProblemAsync(host, BankRelationsUpdateUrl(customer), "{}", ifMatch: null, 428);

        stale.GetProperty("errorCode").GetString().ShouldBe("party.version-conflict");
        missing.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-required");
    }

    [Fact]
    public async Task A_missing_Idempotency_Key_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(BankRelationsUpdateUrl(customer));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(400);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-missing");
    }

    [Fact]
    public async Task Unknown_entries_and_foreign_or_malformed_ids_are_problems()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var unknown = await PutProblemAsync(host, BankRelationsUrl(Guid.CreateVersion7(), Guid.CreateVersion7(), "Update"), "{}", "\"1\"", 404);
        var foreign = await PutProblemAsync(
            host, BankRelationsUrl(customer.PartyReferenceDataDirectoryEntryId, other.BankRelationsId, "Update"), "{}", "\"1\"", 404);
        var malformed = await PutProblemAsync(
            host, BankRelationsUrl(customer.PartyReferenceDataDirectoryEntryId.ToString(), "not-an-id", "Update"), "{}", "\"1\"", 400);

        unknown.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
        foreign.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
        malformed.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        ErrorFields(malformed).ShouldBe(["bankrelationsid"]);
    }
}
