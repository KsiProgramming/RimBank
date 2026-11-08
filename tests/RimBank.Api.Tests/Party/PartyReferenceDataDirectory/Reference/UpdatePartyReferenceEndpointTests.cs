using System.Globalization;
using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.ReferenceScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.10, 1.18 and 1.19: BIAN Reference/Update over HTTP. A partial update of the Reference with an Idempotency-Key and
// the entry's version in If-Match, answered from its acknowledgement with the new version as ETag; the Reference fields
// RimBank does not record yet are refused; every error is Problem Details (specs/tech-stack.md §5, ADR 0024, ADR 0029).
public sealed class UpdatePartyReferenceEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Updating_with_the_current_version_answers_the_normalised_identifiers()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var updated = await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        updated.ShouldBe(Expected(registered));
    }

    [Fact]
    public async Task A_partial_update_keeps_the_identifiers_it_leaves_out_and_clears_the_empty_ones()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        var updated = await PutReferenceOkAsync(
            host,
            UpdateUrl(registered),
            """{ "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": "" } }""",
            "\"2\"",
            "\"3\"");

        updated.GovernmentIssuedIdentityReference.ShouldBe(new GovernmentIssuedIdentityModel { NationalRegistrationNumber = "185057800608436" });
        updated.PartyLegalEntityReference.ShouldBe(new PartyLegalEntityModel { Identification = Lei });
    }

    [Fact]
    public async Task Clearing_every_identifier_leaves_out_both_objects()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        var result = await host.Alba.Scenario(_ =>
        {
            Put(
                _,
                UpdateUrl(registered),
                """
                {
                  "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": "", "NationalRegistrationNumber": " " },
                  "PartyLegalEntityReference": { "Identification": "" }
                }
                """,
                "\"2\"");
            _.StatusCodeShouldBeOk();
        });

        var body = await result.ReadAsJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["PartyReferenceDataDirectoryEntryId", "ReferenceId"]);
    }

    [Fact]
    public async Task Names_date_of_birth_and_residency_are_recorded_normalised_and_kept_by_a_later_identifier_update()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        var updated = await PutReferenceOkAsync(host, UpdateUrl(registered), NamesAndResidency, "\"2\"", "\"3\"");
        var kept = await PutReferenceOkAsync(
            host,
            UpdateUrl(registered),
            """{ "PartyLegalEntityReference": { "Identification": "" } }""",
            "\"3\"",
            "\"4\"");

        updated.ShouldBe(ExpectedWithNamesAndResidency(registered));
        kept.ShouldBe(ExpectedWithNamesAndResidency(registered) with { PartyLegalEntityReference = null });
    }

    [Fact]
    public async Task Addresses_and_contact_points_are_recorded_normalised_and_kept_by_a_later_update()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");
        await PutReferenceOkAsync(host, UpdateUrl(registered), NamesAndResidency, "\"2\"", "\"3\"");

        var updated = await PutReferenceOkAsync(host, UpdateUrl(registered), AddressesAndContactPoints, "\"3\"", "\"4\"");
        var kept = await PutReferenceOkAsync(
            host,
            UpdateUrl(registered),
            """{ "CorporateAddress": {}, "SocialNetworkContacts": { "URLAddress": "" } }""",
            "\"4\"",
            "\"5\"");

        updated.ShouldBe(ExpectedWithAddressesAndContactPoints(registered));
        kept.ShouldBe(ExpectedWithAddressesAndContactPoints(registered) with { CorporateAddress = null, SocialNetworkContacts = null });
    }

    [Fact]
    public async Task Invalid_addresses_and_contact_points_are_a_validation_problem_naming_each_bian_path()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var body = $$"""
            {
              "ResidentialAddress": { "StreetName": "{{new string('S', 36)}}", "Country": { "Code": "FR" } },
              "PartyAddress": { "TownName": "Bruxelles", "Country": { "Code": "ZZ" } },
              "eMailAddress": { "EmailAddress": "jeanne.example.fr" },
              "CellPhoneNumber": { "MobileNumber": "0612345678" },
              "SocialNetworkContacts": { "URLAddress": "http://www.linkedin.com/in/jdupont" }
            }
            """;

        var problem = await PutProblemAsync(host, UpdateUrl(registered), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-invalid");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).ShouldBe(
            [
                "ResidentialAddress.StreetName",
                "ResidentialAddress.TownName",
                "PartyAddress.Country.Code",
                "eMailAddress.EmailAddress",
                "CellPhoneNumber.MobileNumber",
                "SocialNetworkContacts.URLAddress",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task An_unrecorded_address_part_is_refused_without_a_phase_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host,
            UpdateUrl(registered),
            """{ "ResidentialAddress": { "AddressType": "HOME", "TownName": "Paris", "Country": { "Code": "FR" } } }""",
            "\"1\"",
            400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-field-not-supported");
        problem.GetProperty("errors").GetProperty("ResidentialAddress.AddressType")[0].GetString()
            .ShouldBe("RimBank does not record this field.");
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{registered.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Invalid_names_countries_and_residency_are_a_validation_problem_naming_each_bian_field()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var body = $$"""
            {
              "PartyFullLegalName": "{{new string('N', 141)}}",
              "DateofBirth": "17/05/1990",
              "Nationality": { "Code": "ZZ" },
              "PartyCountryOfResidence": { "Code": "FRA" },
              "ResidencyStatus": { "StatusDescription": "Expat" }
            }
            """;

        var problem = await PutProblemAsync(host, UpdateUrl(registered), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-invalid");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).ShouldBe(
            ["PartyFullLegalName", "DateofBirth", "Nationality.Code", "PartyCountryOfResidence.Code", "ResidencyStatus.StatusDescription"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_date_of_birth_before_1900_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, UpdateUrl(registered), """{ "DateofBirth": "1899-12-31" }""", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-invalid");
        problem.GetProperty("errors").GetProperty("DateofBirth")[0].GetString().ShouldBe("The date of birth must not be before 1900-01-01.");
    }

    [Fact]
    public async Task A_date_of_birth_after_today_is_a_validation_problem_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var later = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var problem = await PutProblemAsync(host, UpdateUrl(registered), $$"""{ "DateofBirth": "{{later}}" }""", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-invalid");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).ShouldBe(["DateofBirth"]);
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{registered.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Invalid_identifiers_are_a_validation_problem_naming_each_bian_field()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var body = $$"""
            {
              "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": "{{new string('1', 36)}}" },
              "PartyLegalEntityReference": { "Identification": "5493001KJTIIGC8Y1R13" }
            }
            """;

        var problem = await PutProblemAsync(host, UpdateUrl(registered), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-invalid");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).ShouldBe(
            ["GovernmentIssuedIdentityReference.TaxIdentificationNumber", "PartyLegalEntityReference.Identification"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_reference_field_RimBank_does_not_record_yet_is_refused_with_its_phase_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var body = """
            {
              "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": "FR12345678901", "OtherIdentification": "X1" },
              "Nationality": { "Code": "FR", "Citizen": "Jane" },
              "PartyLegalStructure": "SAS",
              "PartyAddress": null,
              "NotABianField": "ignored"
            }
            """;

        var problem = await PutProblemAsync(host, UpdateUrl(registered), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.reference-field-not-supported");
        var errors = problem.GetProperty("errors");
        errors.EnumerateObject().Select(field => field.Name).ShouldBe(
            ["Nationality.Citizen", "PartyLegalStructure", "GovernmentIssuedIdentityReference.OtherIdentification"],
            ignoreOrder: true);
        errors.GetProperty("Nationality.Citizen")[0].GetString().ShouldBe("RimBank does not record this field.");
        errors.GetProperty("PartyLegalStructure")[0].GetString().ShouldNotBeNull().ShouldContain("1.20");
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{registered.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        var problem = await PutProblemAsync(host, UpdateUrl(registered), "{}", "\"1\"", 409);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.version-conflict");
    }

    [Fact]
    public async Task A_missing_If_Match_is_a_precondition_required_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, UpdateUrl(registered), "{}", ifMatch: null, 428);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-required");
    }

    [Fact]
    public async Task A_weak_If_Match_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, UpdateUrl(registered), "{}", "W/\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-invalid");
    }

    [Fact]
    public async Task An_unknown_entry_is_a_party_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await PutProblemAsync(host, ReferenceUrl(Guid.CreateVersion7(), Guid.CreateVersion7(), "Update"), "{}", "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task Another_entry_s_reference_id_is_a_behavior_qualifier_not_found_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host, ReferenceUrl(registered.PartyReferenceDataDirectoryEntryId, other.ReferenceId, "Update"), "{}", "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
    }

    [Theory]
    [InlineData("not-a-guid", "0198f0a2-5b1c-7000-8000-000000000001", "partyreferencedatadirectoryid")]
    [InlineData("0198f0a2-5b1c-7000-8000-000000000001", "not-a-guid", "referenceid")]
    [InlineData("0198f0a2-5b1c-7000-8000-000000000001", "3f2504e0-4f89-41d3-9a0c-0305e82c3301", "referenceid")]
    public async Task An_id_that_RimBank_never_issues_is_a_validation_problem_naming_it(string entryId, string referenceId, string field)
    {
        await using var host = await server.StartHostAsync();

        var problem = await PutProblemAsync(host, ReferenceUrl(entryId, referenceId, "Update"), "{}", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").GetProperty(field).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_missing_Idempotency_Key_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(UpdateUrl(registered));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(400);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-missing");
    }

    [Fact]
    public async Task The_same_key_and_request_replays_the_first_answer()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var key = Guid.CreateVersion7().ToString();

        var first = await host.Alba.Scenario(_ =>
        {
            Put(_, UpdateUrl(registered), Identifiers, "\"1\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
        var again = await host.Alba.Scenario(_ =>
        {
            Put(_, UpdateUrl(registered), Identifiers, "\"1\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        (await again.ReadAsTextAsync()).ShouldBe(await first.ReadAsTextAsync());
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{registered.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(2);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(ReferenceUrl(Guid.CreateVersion7(), Guid.CreateVersion7(), "Update"));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader("Idempotency-Key", Guid.CreateVersion7().ToString());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }
}
