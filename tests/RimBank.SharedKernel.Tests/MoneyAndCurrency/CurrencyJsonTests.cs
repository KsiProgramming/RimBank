using System.Text.Json;

namespace RimBank.SharedKernel.Tests;

public sealed class CurrencyJsonTests
{
    private sealed record Holder(Currency Currency);

    [Fact]
    public void A_currency_is_written_as_its_code() =>
        JsonSerializer.Serialize(Currency.From("EUR")).ShouldBe("\"EUR\"");

    [Fact]
    public void A_currency_is_read_from_its_code() =>
        JsonSerializer.Deserialize<Currency>("\"BHD\"").ShouldBe(Currency.From("BHD"));

    [Fact]
    public void A_currency_round_trips_inside_a_record() =>
        JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(new Holder(Currency.From("JPY"))))
            .ShouldBe(new Holder(Currency.From("JPY")));

    [Fact]
    public void A_currency_works_as_a_dictionary_key()
    {
        var balances = new Dictionary<Currency, int> { [Currency.From("EUR")] = 1, [Currency.From("USD")] = 2 };

        var json = JsonSerializer.Serialize(balances);

        json.ShouldBe("{\"EUR\":1,\"USD\":2}");
        JsonSerializer.Deserialize<Dictionary<Currency, int>>(json).ShouldBe(balances);
    }

    [Theory]
    [InlineData("\"eur\"")]
    [InlineData("\"XAU\"")]
    [InlineData("\"\"")]
    [InlineData("978")]
    [InlineData("null")]
    [InlineData("{}")]
    public void An_invalid_currency_throws_JsonException(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Currency>(json));

    [Fact]
    public void The_default_currency_cannot_be_written() =>
        Should.Throw<JsonException>(() => JsonSerializer.Serialize(default(Currency)));
}
