namespace RimBank.SharedKernel.MoneyAndCurrency.Tests;

// The usage sample from ADR 0009, kept compiling and passing here.
public sealed class MoneyUsageSampleTests
{
    // Boundary (.Api / .Mcp): untrusted text becomes Money or a Validation error (400 from 0.9).
    private static Result<Money> ParseAmount(string? amount, string? currency) => Money.TryCreate(amount, currency);

    // Decision: a fee is a named rate, rounded explicitly with the mode the product terms name.
    private static Money Fee(Money amount, decimal rate) => (amount * rate).Round(MidpointRounding.AwayFromZero);

    // Decision: two amounts from different requests are combined only when their currencies match.
    private static Result<Money> Total(Money first, Money second) => first.TryAdd(second);

    [Fact]
    public void A_fee_is_calculated_unrounded_then_rounded_with_a_named_mode()
    {
        var result =
            from deposit in ParseAmount("1234.50", "EUR")
            select Fee(deposit, 0.015m);

        result.Value.ToString().ShouldBe("18.52 EUR"); // 18.5175 rounded away from zero
    }

    [Fact]
    public void Amounts_are_combined_only_in_the_same_currency()
    {
        var result =
            from first in ParseAmount("10.00", "EUR")
            from second in ParseAmount("5.00", "USD")
            from total in Total(first, second)
            select total;

        result.Error.Code.ShouldBe("shared.currency-mismatch");
    }

    [Fact]
    public void A_charge_is_split_without_losing_a_cent()
    {
        var charge = ParseAmount("10.00", "EUR").Value;

        var shares = charge.Split(3);

        shares.Select(share => share.ToString()).ShouldBe(["3.34 EUR", "3.33 EUR", "3.33 EUR"]);
        shares.Aggregate(Money.Zero(charge.Currency), (sum, share) => sum + share).ShouldBe(charge);
    }

    [Fact]
    public void Client_input_with_too_many_decimals_is_refused()
    {
        var result = ParseAmount("10.505", "EUR");

        result.Error.Code.ShouldBe("shared.invalid-amount");
        result.Error.FieldErrors.Keys.ShouldBe(["amount"]);
    }
}
