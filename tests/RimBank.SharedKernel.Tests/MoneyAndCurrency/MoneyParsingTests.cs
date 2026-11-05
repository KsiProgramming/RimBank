namespace RimBank.SharedKernel.MoneyAndCurrency.Tests;

public sealed class MoneyParsingTests
{
    [Theory]
    [InlineData("10.50", "EUR", "10.50")]
    [InlineData("10.5", "EUR", "10.5")]
    [InlineData("10", "EUR", "10")]
    [InlineData("0", "EUR", "0")]
    [InlineData("-0.01", "EUR", "-0.01")]
    [InlineData("007", "EUR", "7")]
    [InlineData("1000", "JPY", "1000")]
    [InlineData("1.250", "BHD", "1.250")]
    public void TryCreate_accepts_an_invariant_amount_and_a_currency(string amount, string code, string expected)
    {
        var result = Money.TryCreate(amount, code);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Currency.ShouldBe(Currency.From(code));
    }

    [Theory]
    [InlineData(null, "EUR")]
    [InlineData("", "EUR")]
    [InlineData(" 10", "EUR")]
    [InlineData("10 ", "EUR")]
    [InlineData("10,50", "EUR")]
    [InlineData("1,000.00", "EUR")]
    [InlineData("1 000", "EUR")]
    [InlineData("1e3", "EUR")]
    [InlineData("+10", "EUR")]
    [InlineData(".5", "EUR")]
    [InlineData("5.", "EUR")]
    [InlineData("--5", "EUR")]
    [InlineData("ten", "EUR")]
    [InlineData("10.505", "EUR")] // more decimals than the currency allows
    [InlineData("10.500", "EUR")] // trailing zeros count too
    [InlineData("1.5", "JPY")]
    [InlineData("1.2345", "BHD")]
    [InlineData("99999999999999999999999999999", "EUR")] // above decimal.MaxValue
    public void TryCreate_rejects_an_invalid_amount(string? amount, string code)
    {
        var result = Money.TryCreate(amount, code);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.invalid-amount");
        result.Error.FieldErrors.Keys.ShouldBe(["amount"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("eur")]
    [InlineData("XAU")]
    public void TryCreate_rejects_an_invalid_currency(string? code)
    {
        var result = Money.TryCreate("10.00", code);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.invalid-currency");
        result.Error.FieldErrors.Keys.ShouldBe(["currency"]);
    }

    [Fact]
    public void TryCreate_reports_the_currency_first_when_both_are_invalid() =>
        Money.TryCreate("ten", "eur").Error.Code.ShouldBe("shared.invalid-currency");

    [Fact]
    public void Error_messages_do_not_echo_the_input()
    {
        Money.TryCreate("<script>", "EUR").Error.Message.ShouldNotContain("<script>");
        Money.TryCreate("1", "<script>").Error.Message.ShouldNotContain("<script>");
    }
}
