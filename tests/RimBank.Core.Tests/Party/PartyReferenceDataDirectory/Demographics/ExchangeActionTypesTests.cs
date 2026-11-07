using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading RimBank's ExchangeActionType extension field (ADR 0028).
public sealed class ExchangeActionTypesTests
{
    [Theory]
    [InlineData("Verify", ExchangeActionType.Verify)]
    [InlineData("Reject", ExchangeActionType.Reject)]
    public void The_exact_names_are_the_actions(string name, ExchangeActionType expected) =>
        ExchangeActionTypes.FromName(name).Value.ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_missing_action_is_required(string? name)
    {
        var error = ExchangeActionTypes.FromName(name).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.exchange-action-type-required");
        error.FieldErrors.Keys.ShouldBe(["ExchangeActionType"]);
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("Accept")]
    [InlineData(" Verify")]
    [InlineData("0")]
    public void Any_other_name_is_invalid(string name)
    {
        var error = ExchangeActionTypes.FromName(name).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.exchange-action-type-invalid");
        error.FieldErrors.Keys.ShouldBe(["ExchangeActionType"]);
    }
}
