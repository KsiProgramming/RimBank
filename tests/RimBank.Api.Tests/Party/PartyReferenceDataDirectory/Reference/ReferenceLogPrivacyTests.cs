using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.ReferenceScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.20 (mission quality bar "Security", ADR 0029 "PII", 7.2): the Reference values a caller sends are personal data,
// and some (political exposure, identity documents) are sensitive. Whatever the outcome (accepted, refused as invalid,
// refused as stale) and on Retrieve, none of them reaches the host's logs, even at Debug level.
public sealed class ReferenceLogPrivacyTests(PostgresqlServer server)
{
    // Every submitted Reference value, as sent and as recorded.
    private static readonly string[] _personalValues =
    [
        "FR12345678901",
        "185057800608436",
        "5493001KJTIIGC8Y1R12",
        "Jeanne Marie Dupont",
        "1990-05-17",
        "Rue de Rivoli",
        "Avenue Louise",
        "75001",
        "jeanne@example.fr",
        "+33612345678",
        "+33 6 12 34 56 78",
        "linkedin.com/in/jdupont",
        "Politicalexposuredomestic",
        "Member of a regional council",
        "2015-03-02",
        "23AB45678",
        "DOC-000123",
        "jeanne.dupont.invalid",
        "0611223344",
    ];

    [Fact]
    public async Task No_submitted_reference_value_is_logged_on_success_refusal_conflict_or_retrieve()
    {
        var logs = new CapturedLogs();
        await using var host = await server.StartHostAsync(logs: logs);
        var registered = await RegisterAsync(host);

        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");
        await PutReferenceOkAsync(host, UpdateUrl(registered), NamesAndResidency, "\"2\"", "\"3\"");
        await PutReferenceOkAsync(host, UpdateUrl(registered), AddressesAndContactPoints, "\"3\"", "\"4\"");
        await PutReferenceOkAsync(host, UpdateUrl(registered), LegalStandingAndDocuments, "\"4\"", "\"5\"");
        await PutProblemAsync(
            host,
            UpdateUrl(registered),
            """{ "eMailAddress": { "EmailAddress": "jeanne.dupont.invalid" }, "CellPhoneNumber": { "MobileNumber": "0611223344" } }""",
            "\"5\"",
            400);
        await PutProblemAsync(host, UpdateUrl(registered), LegalStandingAndDocuments, "\"1\"", 409);
        await host.Alba.Scenario(_ =>
        {
            _.Get.Url(RetrieveUrl(registered));
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.StatusCodeShouldBeOk();
        });

        // The capture sees the requests themselves (their path holds no Reference value), so an empty match means not logged.
        logs.Containing($"/Reference/{registered.ReferenceId}/Update").ShouldNotBeEmpty();
        foreach (var value in _personalValues)
        {
            logs.Containing(value).Select(entry => $"{entry.Level} {entry.Category}").ShouldBeEmpty($"'{value}' was logged");
        }
    }

    // Roadmap 1.21 (ADR 0034): who the company's officer is, and in which role, is personal data too. Neither the referenced
    // entries nor the role reach the logs, on success, on a refused reference or on Retrieve.
    [Fact]
    public async Task No_submitted_corporate_reference_or_role_is_logged()
    {
        var logs = new CapturedLogs();
        await using var host = await server.StartHostAsync(logs: logs);
        var company = await RegisterAsync(host, "Organisation");
        var group = await RegisterAsync(host, "Organisation");
        var officer = await RegisterAsync(host);
        var references = $$"""
            {
              "CorporateCustomerReference": { "PartyReference": "{{group.PartyReferenceDataDirectoryEntryId}}" },
              "CompanyOfficerReference": { "PartyReference": "{{officer.PartyReferenceDataDirectoryEntryId}}" },
              "CompanyOfficerRole": "Chief Risk Officer"
            }
            """;

        await PutReferenceOkAsync(host, UpdateUrl(company), references, "\"1\"", "\"2\"");
        await PutProblemAsync(
            host,
            UpdateUrl(company),
            $$"""{ "CompanyOfficerReference": { "PartyReference": "{{group.PartyReferenceDataDirectoryEntryId}}" } }""",
            "\"2\"",
            422);
        await host.Alba.Scenario(_ =>
        {
            _.Get.Url(RetrieveUrl(company));
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.StatusCodeShouldBeOk();
        });

        logs.Containing($"/Reference/{company.ReferenceId}/Update").ShouldNotBeEmpty();
        foreach (var value in new[] { group.PartyReferenceDataDirectoryEntryId.ToString(), officer.PartyReferenceDataDirectoryEntryId.ToString(), "Chief Risk Officer" })
        {
            logs.Containing(value).Select(entry => $"{entry.Level} {entry.Category}").ShouldBeEmpty($"'{value}' was logged");
        }
    }
}
