namespace RimBank.SharedKernel.MoneyAndCurrency.Tests;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("EUR", 978, 2)]
    [InlineData("USD", 840, 2)]
    [InlineData("GBP", 826, 2)]
    [InlineData("JPY", 392, 0)]
    [InlineData("KRW", 410, 0)]
    [InlineData("BHD", 48, 3)]
    [InlineData("KWD", 414, 3)]
    [InlineData("TND", 788, 3)]
    [InlineData("XAF", 950, 0)]
    [InlineData("XOF", 952, 0)]
    [InlineData("XCD", 951, 2)]
    public void TryCreate_finds_the_currency_with_its_iso_4217_data(string code, int numericCode, int minorUnits)
    {
        var result = Currency.TryCreate(code);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe(code);
        result.Value.NumericCode.ShouldBe(numericCode);
        result.Value.MinorUnits.ShouldBe(minorUnits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eur")]
    [InlineData("Eur")]
    [InlineData(" EUR")]
    [InlineData("EUR ")]
    [InlineData("EURO")]
    [InlineData("EU")]
    [InlineData("978")]
    [InlineData("ABC")]
    [InlineData("XAU")] // metal, no minor units
    [InlineData("XDR")] // special drawing right, no minor units
    [InlineData("XTS")] // reserved for testing
    [InlineData("XXX")] // no currency
    [InlineData("CLF")] // fund code, 4 minor units
    [InlineData("XAD")] // fund code
    [InlineData("HRK")] // withdrawn in 2023
    public void TryCreate_rejects_anything_that_is_not_a_currency_in_the_table(string? code)
    {
        var result = Currency.TryCreate(code);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.invalid-currency");
        result.Error.ShouldBe(Currency.InvalidCurrency);
    }

    [Fact]
    public void From_returns_the_same_currency_as_TryCreate() =>
        Currency.From("EUR").ShouldBe(Currency.TryCreate("EUR").Value);

    [Theory]
    [InlineData("eur")]
    [InlineData("XAU")]
    [InlineData("")]
    public void From_throws_for_a_code_that_is_not_in_the_table(string code) =>
        Should.Throw<ArgumentException>(() => Currency.From(code)).Message.ShouldContain("ISO 4217");

    [Fact]
    public void From_requires_a_code() =>
        Should.Throw<ArgumentNullException>(() => Currency.From(null!));

    [Fact]
    public void Currencies_with_the_same_code_are_equal()
    {
        Currency.From("EUR").ShouldBe(Currency.From("EUR"));
        (Currency.From("EUR") == Currency.From("EUR")).ShouldBeTrue();
        Currency.From("EUR").GetHashCode().ShouldBe(Currency.From("EUR").GetHashCode());
        Currency.From("EUR").ShouldNotBe(Currency.From("USD"));
    }

    [Fact]
    public void ToString_is_the_code() => Currency.From("JPY").ToString().ShouldBe("JPY");

    [Fact]
    public void The_default_currency_is_not_a_currency()
    {
        default(Currency).Code.ShouldBeNull();
        default(Currency).ToString().ShouldBe(string.Empty);
        Currency.All.ShouldNotContain(default(Currency));
    }
}
