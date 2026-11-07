using System.Text.Json;

namespace RimBank.SharedKernel.Tests;

public sealed class MoneyJsonTests
{
    private sealed record Holder(Money Balance, Money? Limit);

    private static Money Of(decimal amount, string code = "EUR") => Money.Of(amount, Currency.From(code));

    [Fact]
    public void Money_is_written_with_the_amount_as_a_string() =>
        JsonSerializer.Serialize(Of(10.5m)).ShouldBe("{\"amount\":\"10.50\",\"currency\":\"EUR\"}");

    [Fact]
    public void Money_is_read_from_an_amount_and_a_currency() =>
        JsonSerializer.Deserialize<Money>("{\"amount\":\"10.50\",\"currency\":\"EUR\"}").ShouldBe(Of(10.50m));

    [Fact]
    public void Properties_may_come_in_any_order_and_unknown_ones_are_ignored() =>
        JsonSerializer.Deserialize<Money>("{\"note\":{\"x\":[1]},\"currency\":\"JPY\",\"amount\":\"1000\"}")
            .ShouldBe(Of(1000m, "JPY"));

    [Fact]
    public void An_unrounded_amount_keeps_its_decimals()
    {
        var accrued = Of(0.0123456m);

        var json = JsonSerializer.Serialize(accrued);

        json.ShouldBe("{\"amount\":\"0.0123456\",\"currency\":\"EUR\"}");
        JsonSerializer.Deserialize<Money>(json).Amount.ShouldBe(0.0123456m);
    }

    [Fact]
    public void Money_round_trips_inside_a_record_including_null()
    {
        var holder = new Holder(Of(-3.20m), null);

        JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(holder)).ShouldBe(holder);
    }

    [Theory]
    [InlineData("\"10.50 EUR\"")]
    [InlineData("10.5")]
    [InlineData("{\"amount\":10.50,\"currency\":\"EUR\"}")] // a JSON number, not a string
    [InlineData("{\"amount\":\"10.50\"}")]
    [InlineData("{\"currency\":\"EUR\"}")]
    [InlineData("{\"amount\":\"10,50\",\"currency\":\"EUR\"}")]
    [InlineData("{\"amount\":\"1e3\",\"currency\":\"EUR\"}")]
    [InlineData("{\"amount\":\"10.50\",\"currency\":\"eur\"}")]
    [InlineData("{\"amount\":\"10.50\",\"currency\":978}")]
    public void Invalid_money_throws_JsonException(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Money>(json));

    [Fact]
    public void The_default_money_cannot_be_written() =>
        Should.Throw<JsonException>(() => JsonSerializer.Serialize(default(Money)));
}
