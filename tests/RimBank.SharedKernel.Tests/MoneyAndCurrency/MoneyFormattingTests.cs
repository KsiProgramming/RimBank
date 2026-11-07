using System.Globalization;

namespace RimBank.SharedKernel.Tests;

public sealed class MoneyFormattingTests
{
    private static Money Of(decimal amount, string code) => Money.Of(amount, Currency.From(code));

    [Theory]
    [InlineData(10.5, "EUR", "10.50 EUR")]
    [InlineData(10, "EUR", "10.00 EUR")]
    [InlineData(-0.01, "EUR", "-0.01 EUR")]
    [InlineData(1000, "JPY", "1000 JPY")]
    [InlineData(1.25, "BHD", "1.250 BHD")]
    [InlineData(0.12345, "EUR", "0.12345 EUR")] // unrounded amounts keep their decimals
    [InlineData(1234567.89, "EUR", "1234567.89 EUR")] // no grouping separators
    public void ToString_shows_the_amount_with_at_least_the_minor_units_and_the_code(decimal amount, string code, string expected) =>
        Of(amount, code).ToString().ShouldBe(expected);

    [Fact]
    public void FormatAmount_is_the_amount_alone() => Of(10.5m, "EUR").FormatAmount().ShouldBe("10.50");

    [Fact]
    public void Zero_is_never_negative() => (-Of(0m, "EUR")).FormatAmount().ShouldBe("0.00");

    [Fact]
    public void Formatting_ignores_the_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Of(1234.5m, "EUR").ToString().ShouldBe("1234.50 EUR");
            Money.TryCreate("1234.50", "EUR").Value.ShouldBe(Of(1234.5m, "EUR"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void The_default_money_has_an_empty_text() => default(Money).ToString().ShouldBe(string.Empty);
}
