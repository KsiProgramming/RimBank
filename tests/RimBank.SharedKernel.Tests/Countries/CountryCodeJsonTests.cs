using System.Text.Json;

namespace RimBank.SharedKernel.Tests;

public sealed class CountryCodeJsonTests
{
    private sealed record Holder(CountryCode Country, CountryCode? Optional);

    [Fact]
    public void A_country_is_written_as_its_code() =>
        JsonSerializer.Serialize(CountryCode.From("FR")).ShouldBe("\"FR\"");

    [Fact]
    public void A_country_is_read_from_its_code() =>
        JsonSerializer.Deserialize<CountryCode>("\"BE\"").ShouldBe(CountryCode.From("BE"));

    [Fact]
    public void A_country_round_trips_inside_a_record()
    {
        var holder = new Holder(CountryCode.From("JP"), null);

        JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(holder)).ShouldBe(holder);
    }

    [Fact]
    public void A_country_works_as_a_dictionary_key()
    {
        var counts = new Dictionary<CountryCode, int> { [CountryCode.From("FR")] = 1, [CountryCode.From("US")] = 2 };

        var json = JsonSerializer.Serialize(counts);

        json.ShouldBe("{\"FR\":1,\"US\":2}");
        JsonSerializer.Deserialize<Dictionary<CountryCode, int>>(json).ShouldBe(counts);
    }

    [Theory]
    [InlineData("\"fr\"")]
    [InlineData("\"ZZ\"")]
    [InlineData("\"\"")]
    [InlineData("250")]
    [InlineData("null")]
    [InlineData("{}")]
    public void An_invalid_country_throws_JsonException(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CountryCode>(json));

    [Fact]
    public void The_default_country_cannot_be_written() =>
        Should.Throw<JsonException>(() => JsonSerializer.Serialize(default(CountryCode)));
}
