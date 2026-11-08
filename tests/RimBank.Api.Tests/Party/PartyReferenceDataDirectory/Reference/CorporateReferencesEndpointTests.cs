using Alba;
using Microsoft.Net.Http.Headers;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.AssociationScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.ReferenceScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.21: the corporate and officer references over HTTP. An organisation's Reference names other registered
// entries (two organisations and a person), checked on the write side; a broken rule is a 422 Problem Details and nothing
// is stored; Retrieve shows what Update recorded (specs/tech-stack.md §5, ADR 0034).
public sealed class CorporateReferencesEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task An_organisation_records_its_references_and_retrieve_shows_them()
    {
        await using var host = await server.StartHostAsync();
        var company = await RegisterAsync(host, "Organisation");
        var group = await RegisterAsync(host, "Organisation");
        var officer = await RegisterAsync(host);

        var updated = await PutReferenceOkAsync(
            host,
            UpdateUrl(company),
            References(group.PartyReferenceDataDirectoryEntryId, officer.PartyReferenceDataDirectoryEntryId),
            "\"1\"",
            "\"2\"");

        var expected = new PartyReferenceResponse(company.PartyReferenceDataDirectoryEntryId, company.ReferenceId, null, null) with
        {
            CorporateCustomerReference = new InvolvedPartyModel { PartyReference = group.PartyReferenceDataDirectoryEntryId.ToString() },
            CorporateCustomerLegalEntityReference = new InvolvedPartyModel { PartyReference = group.PartyReferenceDataDirectoryEntryId.ToString() },
            CompanyOfficerReference = new InvolvedPartyModel { PartyReference = officer.PartyReferenceDataDirectoryEntryId.ToString() },
            CompanyOfficerRole = "Director",
        };
        updated.ShouldBe(expected);
        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, RetrieveUrl(company));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
        (await result.ReadAsJsonAsync<PartyReferenceResponse>()).ShouldBe(expected);
    }

    [Fact]
    public async Task A_reference_to_an_entry_that_is_not_registered_is_a_422_problem_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var company = await RegisterAsync(host, "Organisation");

        var problem = await PutProblemAsync(
            host,
            UpdateUrl(company),
            $$"""{ "CorporateCustomerReference": { "PartyReference": "{{Guid.CreateVersion7()}}" } }""",
            "\"1\"",
            422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.referenced-party-not-registered");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldStartWith("CorporateCustomerReference.PartyReference");
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{company.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task A_person_as_the_corporate_customer_is_a_422_type_mismatch()
    {
        await using var host = await server.StartHostAsync();
        var company = await RegisterAsync(host, "Organisation");
        var person = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host,
            UpdateUrl(company),
            $$"""{ "CorporateCustomerReference": { "PartyReference": "{{person.PartyReferenceDataDirectoryEntryId}}" } }""",
            "\"1\"",
            422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.referenced-party-type-mismatch");
    }

    [Fact]
    public async Task A_malformed_party_reference_is_a_400_naming_its_path()
    {
        await using var host = await server.StartHostAsync();
        var company = await RegisterAsync(host, "Organisation");

        var problem = await PutProblemAsync(host, UpdateUrl(company), """{ "CompanyOfficerReference": { "PartyReference": "ACME-1" } }""", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-invalid");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).ShouldBe(["CompanyOfficerReference.PartyReference"]);
    }

    [Fact]
    public async Task An_organisation_with_references_cannot_become_a_person()
    {
        await using var host = await server.StartHostAsync();
        var company = await RegisterAsync(host, "Organisation");
        var group = await RegisterAsync(host, "Organisation");
        var officer = await RegisterAsync(host);
        await PutReferenceOkAsync(
            host,
            UpdateUrl(company),
            References(group.PartyReferenceDataDirectoryEntryId, officer.PartyReferenceDataDirectoryEntryId),
            "\"1\"",
            "\"2\"");

        var problem = await PutProblemAsync(
            host,
            $"/PartyReferenceDataDirectory/{company.PartyReferenceDataDirectoryEntryId}/Update",
            """{ "PartyType": "Person" }""",
            "\"2\"",
            422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.corporate-references-need-organisation");
    }

    private static string References(Guid organisation, Guid person) =>
        $$"""
        {
          "CorporateCustomerReference": { "PartyReference": "{{organisation}}" },
          "CorporateCustomerLegalEntityReference": { "PartyReference": "{{organisation}}" },
          "CompanyOfficerReference": { "PartyReference": "{{person}}" },
          "CompanyOfficerRole": " Director "
        }
        """;
}
