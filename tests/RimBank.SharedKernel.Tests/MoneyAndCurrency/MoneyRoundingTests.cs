using System.Globalization;

namespace RimBank.SharedKernel.MoneyAndCurrency.Tests;

public sealed class MoneyRoundingTests
{
    private static Money Of(decimal amount, string code) => Money.Of(amount, Currency.From(code));

    [Theory]
    [InlineData(2.345, 2.34)] // midpoint: to the even neighbour
    [InlineData(2.355, 2.36)]
    [InlineData(-2.345, -2.34)]
    [InlineData(2.3451, 2.35)] // not a midpoint
    [InlineData(2.344, 2.34)]
    public void Round_uses_bankers_rounding_by_default(decimal amount, decimal expected) =>
        Of(amount, "EUR").Round().ShouldBe(Of(expected, "EUR"));

    [Theory]
    [InlineData(2.345, 2.35)]
    [InlineData(-2.345, -2.35)]
    public void Round_uses_the_named_mode(decimal amount, decimal expected) =>
        Of(amount, "EUR").Round(MidpointRounding.AwayFromZero).ShouldBe(Of(expected, "EUR"));

    [Fact]
    public void Round_follows_the_minor_units_of_the_currency()
    {
        Of(1234.5m, "JPY").Round().ShouldBe(Of(1234m, "JPY"));
        Of(1235.5m, "JPY").Round().ShouldBe(Of(1236m, "JPY"));
        Of(1.2345m, "BHD").Round().ShouldBe(Of(1.234m, "BHD"));
        Of(1.2355m, "BHD").Round().ShouldBe(Of(1.236m, "BHD"));
    }

    [Theory]
    [InlineData("10", "EUR", true)]
    [InlineData("10.5", "EUR", true)]
    [InlineData("10.50", "EUR", true)]
    [InlineData("10.500", "EUR", true)] // trailing zeros are not extra precision
    [InlineData("10.505", "EUR", false)]
    [InlineData("10", "JPY", true)]
    [InlineData("10.5", "JPY", false)]
    [InlineData("1.234", "BHD", true)]
    [InlineData("1.2345", "BHD", false)]
    public void IsRoundedToMinorUnits_tells_whether_rounding_would_change_the_amount(string amount, string code, bool rounded) =>
        Of(decimal.Parse(amount, CultureInfo.InvariantCulture), code).IsRoundedToMinorUnits.ShouldBe(rounded);
}
