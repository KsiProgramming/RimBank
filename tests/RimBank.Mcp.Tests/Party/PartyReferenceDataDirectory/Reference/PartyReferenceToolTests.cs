using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the Reference tools send the same Update and Retrieve as the HTTP endpoints, with expectedVersion in
// place of If-Match and the BIAN UpdateReference body as their request argument (ADR 0029, ADR 0034, ADR 0039, ADR 0040).
public sealed class PartyReferenceToolTests(PostgresqlServer server)
{
    internal const string Person =
        """
        {
          "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": " FR12345678901 ", "NationalRegistrationNumber": "185057800608436" },
          "PartyFullLegalName": " Jeanne Marie Dupont ",
          "PartyNameSalutation": "Ms",
          "DateofBirth": "1990-05-17",
          "Nationality": { "Code": "fr" },
          "PartyCountryOfResidence": { "Code": "BE" },
          "ResidencyStatus": { "StatusDescription": "nonresident", "StatusDateTime": "2024-01-01T00:00:00Z" },
          "ResidentialAddress": {
            "StreetName": " Rue de Rivoli ",
            "StreetBuildingIdentification": "12",
            "PostCodeIdentification": "75001",
            "TownName": "Paris",
            "Country": { "Code": "fr" }
          },
          "eMailAddress": { "EmailAddress": " jeanne@example.fr " },
          "CellPhoneNumber": { "MobileNumber": "+33 6 12 34 56 78" },
          "SocialNetworkContacts": { "URLAddress": "https://www.linkedin.com/in/jdupont" },
          "PoliticalExposureType": "Politicalexposuredomestic",
          "PoliticalExposureDescriptionRecord": " Member of a regional council since 2021 ",
          "CustomerSinceDate": "2015-03-02T10:00:00+01:00",
          "GovernmentIssuedDocumentDetails": {
            "PartyDocumentType": "PersonalDocument",
            "DocumentReference": {
              "DocumentIdentification": { "IdentifierValue": "23AB45678" },
              "DocumentName": "Passport",
              "DocumentValidityPeriod": {
                "FromDateTime": { "DateTimeContent": "2023-04-01" },
                "ToDateTime": { "DateTimeContent": "2033-03-31T00:00:00Z" }
              }
            }
          },
          "DocumentDirectoryEntryInstanceReference": { "DocumentDirectoryEntryIdentification": " DOC-000123 " }
        }
        """;

    [Fact]
    public async Task Update_then_retrieve_answers_the_reference_and_the_versions()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        var updated = (await UpdateAsync(host, entry, Person, entry.Version, PartyTools.Key("update"))).Success();
        var retrieved = (await RetrieveAsync(host, entry.Id.ToString(), entry.ReferenceId.ToString())).Success();

        updated.GetProperty("PartyFullLegalName").GetString().ShouldBe("Jeanne Marie Dupont");
        updated.GetProperty("Nationality").GetProperty("Code").GetString().ShouldBe("FR");
        updated.GetProperty("ResidentialAddress").GetProperty("TownName").GetString().ShouldBe("Paris");
        updated.GetProperty("CustomerSinceDate").GetString().ShouldBe("2015-03-02");
        updated.GetProperty("version").GetInt64().ShouldBe(entry.Version + 1);
        JsonElement.DeepEquals(retrieved, updated).ShouldBeTrue($"{retrieved} vs {updated}");
    }

    [Fact]
    public async Task A_left_out_field_keeps_its_value_an_empty_one_clears_it_and_an_address_is_replaced_whole()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var first = (await UpdateAsync(host, entry, Person, entry.Version, PartyTools.Key("first"))).Success();

        var second = (await UpdateAsync(
                host,
                entry,
                """{ "PartyNameSalutation": "", "ResidentialAddress": { "TownName": "Lyon", "Country": { "Code": "FR" } } }""",
                first.GetProperty("version").GetInt64(),
                PartyTools.Key("second")))
            .Success();

        second.GetProperty("PartyFullLegalName").GetString().ShouldBe("Jeanne Marie Dupont");
        second.TryGetProperty("PartyNameSalutation", out _).ShouldBeFalse();
        var address = second.GetProperty("ResidentialAddress");
        address.GetProperty("TownName").GetString().ShouldBe("Lyon");
        address.TryGetProperty("StreetName", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{ "PartyLegalEntityReference": { "Identification": "5493001KJTIIGC8Y1R13" } }""")]
    [InlineData("""{ "eMailAddress": { "EmailAddress": "not-an-email" } }""")]
    [InlineData("""{ "Nationality": { "Code": "XX" } }""")]
    public async Task An_invalid_value_is_a_validation_problem(string request)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        (await UpdateAsync(host, entry, request, entry.Version, PartyTools.Key("invalid"))).Problem(400, "party.reference-invalid");
        (await host.ScalarAsync<long>(PartyTools.EventsOf(entry.Id))).ShouldBe(1);
    }

    [Fact]
    public async Task A_part_rimbank_does_not_record_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        (await UpdateAsync(host, entry, """{ "eMailAddress": { "EmailAddress": "jeanne@example.fr", "Identification": "x" } }""", entry.Version, PartyTools.Key("refused")))
            .Problem(400, "party.reference-field-not-supported");
    }

    [Fact]
    public async Task Corporate_and_officer_references_are_recorded_on_an_organisation_only()
    {
        await using var host = await server.StartHostAsync();
        var company = await Entry.RegisterAsync(host, "Organisation");
        var person = await Entry.RegisterAsync(host, "Person");
        var group = await Entry.RegisterAsync(host, "Organisation");
        var references = Corporate(group.Id, person.Id);

        var recorded = (await UpdateAsync(host, company, references, company.Version, PartyTools.Key("company"))).Success();
        (await UpdateAsync(host, person, references, person.Version, PartyTools.Key("person")))
            .Problem(422, "party.corporate-references-need-organisation");

        recorded.GetProperty("CompanyOfficerReference").GetProperty("PartyReference").GetGuid().ShouldBe(person.Id);
        recorded.GetProperty("CompanyOfficerRole").GetString().ShouldBe("Director");
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        (await UpdateAsync(host, entry, "{}", entry.Version, PartyTools.Key("first"))).Success();

        (await UpdateAsync(host, entry, "{}", entry.Version, PartyTools.Key("stale")))
            .Problem(409, PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code);
    }

    internal static string Corporate(Guid organisation, Guid person) =>
        $$"""
        {
          "CorporateCustomerReference": { "PartyReference": "{{organisation}}" },
          "CorporateCustomerLegalEntityReference": { "PartyReference": "{{organisation}}" },
          "CompanyOfficerReference": { "PartyReference": "{{person}}" },
          "CompanyOfficerRole": " Director "
        }
        """;

    internal static Task<CallToolResult> UpdateAsync(PartyMcpHost host, Entry entry, string request, long expectedVersion, string key) =>
        host.CallAsync(
            UpdatePartyReferenceTool.Name,
            new()
            {
                ["partyReferenceDataDirectoryEntryId"] = entry.Id.ToString(),
                ["referenceId"] = entry.ReferenceId.ToString(),
                ["request"] = JsonElement.Parse(request),
                ["expectedVersion"] = expectedVersion,
                ["idempotencyKey"] = key,
            });

    internal static Task<CallToolResult> RetrieveAsync(PartyMcpHost host, string entryId, string referenceId) =>
        host.CallAsync(
            RetrievePartyReferenceTool.Name,
            new() { ["partyReferenceDataDirectoryEntryId"] = entryId, ["referenceId"] = referenceId });
}
