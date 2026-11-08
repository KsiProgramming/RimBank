using Alba;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.BankRelationsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.12 (mission quality bar "Security", ADR 0035, 7.2): who manages a party at the bank, since when and how the
// relationship stands is personal data. Whatever the outcome (accepted, refused as invalid, refused by a rule, refused as
// stale) and on Retrieve, none of the submitted values reaches the host's logs, even at Debug level.
public sealed class BankRelationsLogPrivacyTests(PostgresqlServer server)
{
    [Fact]
    public async Task No_submitted_bank_relations_value_is_logged_on_success_refusal_conflict_or_retrieve()
    {
        var logs = new CapturedLogs();
        await using var host = await server.StartHostAsync(logs: logs);
        var customer = await RegisterAsync(host);
        var manager = await RegisterAsync(host);
        var branch = await RegisterAsync(host, "Organisation");
        var managed = $$"""
            {
              "BankRelationType": "RelationshipManager",
              "BusinessUnitEmployeeReference": { "PartyReference": "{{manager.PartyReferenceDataDirectoryEntryId}}" },
              "BankRelationReference": {
                "RelationshipStartDate": { "DateTimeContent": "2019-04-23" },
                "RelationshipEndDate": { "DateTimeContent": "2031-08-14T00:00:00Z" },
                "RelationshipLifecycleStatus": { "StatusDescription": "Suspended" }
              }
            }
            """;

        await PutBankRelationsOkAsync(host, BankRelationsUpdateUrl(customer), managed, "\"1\"", "\"2\"");
        await PutProblemAsync(
            host,
            BankRelationsUpdateUrl(customer),
            """{ "BankRelationReference": { "RelationshipStartDate": { "DateTimeContent": "23/04/2019-x" } } }""",
            "\"2\"",
            400);
        await PutProblemAsync(host, BankRelationsUpdateUrl(customer), ManagedBody(branch.PartyReferenceDataDirectoryEntryId), "\"2\"", 422);
        await PutProblemAsync(host, BankRelationsUpdateUrl(customer), managed, "\"1\"", 409);
        await host.Alba.Scenario(_ =>
        {
            AssociationScenarios.Get(_, BankRelationsRetrieveUrl(customer));
            _.StatusCodeShouldBeOk();
        });

        // The capture sees the requests themselves (their path holds no BankRelations value), so an empty match means not logged.
        logs.Containing($"/BankRelations/{customer.BankRelationsId}/Update").ShouldNotBeEmpty();
        string[] values =
        [
            manager.PartyReferenceDataDirectoryEntryId.ToString(),
            branch.PartyReferenceDataDirectoryEntryId.ToString(),
            "2019-04-23",
            "2031-08-14",
            "Suspended",
            "23/04/2019-x",
        ];
        foreach (var value in values)
        {
            logs.Containing(value).Select(entry => $"{entry.Level} {entry.Category}").ShouldBeEmpty($"'{value}' was logged");
        }
    }
}
