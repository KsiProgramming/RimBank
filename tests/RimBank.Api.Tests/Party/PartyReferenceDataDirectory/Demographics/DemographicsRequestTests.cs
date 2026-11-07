using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.9: the BIAN Demographics Update and Exchange bodies become their commands, with the route's ids and the
// If-Match version (ADR 0024, ADR 0028).
public sealed class DemographicsRequestTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.From("demographics-1");
    private static readonly PartyReferenceDataDirectoryEntryId _entryId = PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7());
    private static readonly DemographicsId _demographicsId = DemographicsId.From(Guid.CreateVersion7());

    [Fact]
    public void The_BIAN_update_fields_become_trimmed_changes_and_EthnicityReligion_is_dropped()
    {
        var request = JsonSerializer.Deserialize<UpdateDemographicsRequest>(
            """
            {
              "Socio-EconomicClassification": " B2 ",
              "EthnicityReligion": "never stored",
              "Employment": { "PartyReference": "Contoso Ltd", "InvolvementReference": "Accountant" },
              "EmploymentHistory": "Contoso Ltd since 2020",
              "EducationHistory": "",
              "ServicingConstraints": "Large print"
            }
            """,
            JsonSerializerOptions.Web).ShouldNotBeNull();

        request.ToCommand(_entryId, _demographicsId, 4, _key).Value.ShouldBe(new UpdateDemographics(
            _entryId,
            _demographicsId,
            4,
            new DemographicsChanges("B2", new Employment("Contoso Ltd", "Accountant"), "Contoso Ltd since 2020", "", "Large print"),
            _key));
    }

    [Fact]
    public void An_empty_update_body_changes_nothing() =>
        new UpdateDemographicsRequest().ToCommand(_entryId, _demographicsId, 1, _key)
            .Value.Changes.ShouldBe(DemographicsChanges.None);

    [Fact]
    public void A_text_that_is_too_long_is_a_validation_error_naming_the_BIAN_field()
    {
        var command = new UpdateDemographicsRequest { SocioEconomicClassification = new string('a', 141) }
            .ToCommand(_entryId, _demographicsId, 1, _key);

        command.Error.Code.ShouldBe("party.demographics-invalid");
        command.Error.FieldErrors.Keys.ShouldBe(["Socio-EconomicClassification"]);
    }

    [Theory]
    [InlineData("Verify", ExchangeActionType.Verify)]
    [InlineData("Reject", ExchangeActionType.Reject)]
    public void The_exchange_action_becomes_the_command_and_the_BIAN_detail_fields_are_ignored(string action, ExchangeActionType expected)
    {
        var request = JsonSerializer.Deserialize<ExchangeDemographicsRequest>(
            $$"""
            {
              "ExchangeActionType": "{{action}}",
              "Socio-EconomicClassification": "ignored",
              "EthnicityReligion": "ignored",
              "Employment": { "PartyReference": "ignored" },
              "EmploymentHistory": "ignored",
              "EducationHistory": "ignored",
              "ServicingConstraints": "ignored"
            }
            """,
            JsonSerializerOptions.Web).ShouldNotBeNull();

        request.ToCommand(_entryId, _demographicsId, 2, _key).Value
            .ShouldBe(new ExchangeDemographics(_entryId, _demographicsId, 2, expected, _key));
    }

    [Theory]
    [InlineData(null, "party.exchange-action-type-required")]
    [InlineData("", "party.exchange-action-type-required")]
    [InlineData("verify", "party.exchange-action-type-invalid")]
    [InlineData("Accept", "party.exchange-action-type-invalid")]
    public void A_missing_or_unknown_exchange_action_is_a_validation_error(string? action, string code)
    {
        var command = new ExchangeDemographicsRequest { ExchangeActionType = action }.ToCommand(_entryId, _demographicsId, 1, _key);

        command.Error.Code.ShouldBe(code);
        command.Error.FieldErrors.Keys.ShouldBe(["ExchangeActionType"]);
    }
}
