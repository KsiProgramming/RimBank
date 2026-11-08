namespace RimBank.SharedKernel.Tests;

public sealed class CountryCodeTests
{
    [Theory]
    [InlineData("FR", 250)]
    [InlineData("BE", 56)]
    [InlineData("DE", 276)]
    [InlineData("GB", 826)]
    [InlineData("US", 840)]
    [InlineData("MA", 504)]
    [InlineData("JP", 392)]
    [InlineData("AX", 248)]
    [InlineData("SS", 728)]
    public void TryCreate_finds_the_country_with_its_iso_3166_numeric_code(string code, int numericCode)
    {
        var result = CountryCode.TryCreate(code);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe(code);
        result.Value.NumericCode.ShouldBe(numericCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("Fr")]
    [InlineData(" FR")]
    [InlineData("FR ")]
    [InlineData("FRA")] // alpha-3
    [InlineData("F")]
    [InlineData("250")] // numeric
    [InlineData("ZZ")] // user-assigned
    [InlineData("XK")] // user-assigned (Kosovo)
    [InlineData("EU")] // exceptionally reserved
    [InlineData("UK")] // exceptionally reserved
    [InlineData("AN")] // transitionally reserved (Netherlands Antilles)
    public void TryCreate_rejects_anything_that_is_not_a_country_in_the_table(string? code)
    {
        var result = CountryCode.TryCreate(code);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.invalid-country");
        result.Error.ShouldBe(CountryCode.InvalidCountry);
    }

    [Fact]
    public void From_returns_the_same_country_as_TryCreate() =>
        CountryCode.From("FR").ShouldBe(CountryCode.TryCreate("FR").Value);

    [Theory]
    [InlineData("fr")]
    [InlineData("ZZ")]
    public void From_throws_for_a_code_that_is_not_in_the_table(string code) =>
        Should.Throw<ArgumentException>(() => CountryCode.From(code));

    [Fact]
    public void Countries_with_the_same_code_are_equal()
    {
        CountryCode.From("FR").ShouldBe(CountryCode.From("FR"));
        CountryCode.From("FR").ShouldNotBe(CountryCode.From("BE"));
    }

    [Fact]
    public void ToString_is_the_code()
    {
        CountryCode.From("FR").ToString().ShouldBe("FR");
        default(CountryCode).ToString().ShouldBe(string.Empty);
    }
}
