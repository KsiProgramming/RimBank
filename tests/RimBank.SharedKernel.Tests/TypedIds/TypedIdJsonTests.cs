using System.Text.Json;

namespace RimBank.SharedKernel.TypedIds.Tests;

public sealed class TypedIdJsonTests
{
    private const string Text = "019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f";

    private static readonly SampleId _id = SampleId.From(Guid.ParseExact(Text, "D"));

    private static readonly JsonSerializerOptions _options = new() { Converters = { new TypedIdJsonConverterFactory() } };

    [Fact]
    public void An_identifier_inside_a_record_round_trips()
    {
        var holder = new Holder(_id, null);

        var json = JsonSerializer.Serialize(holder, _options);

        json.ShouldBe($$"""{"Id":"{{Text}}","Optional":null}""");
        JsonSerializer.Deserialize<Holder>(json, _options).ShouldBe(holder);
    }

    [Fact]
    public void A_nullable_identifier_with_a_value_round_trips()
    {
        var holder = new Holder(_id, _id);

        JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(holder, _options), _options).ShouldBe(holder);
    }

    [Fact]
    public void An_identifier_can_be_a_dictionary_key()
    {
        var counts = new Dictionary<SampleId, int> { [_id] = 3 };

        var json = JsonSerializer.Serialize(counts, _options);

        json.ShouldBe($$"""{"{{Text}}":3}""");
        JsonSerializer.Deserialize<Dictionary<SampleId, int>>(json, _options)!.ShouldBe(counts);
    }

    [Theory]
    [InlineData("\"not-an-identifier\"")]
    [InlineData("\"3f2504e0-4f89-41d3-9a0c-0305e82c3301\"")] // version 4
    [InlineData("\"00000000-0000-0000-0000-000000000000\"")] // nil
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("null")]
    public void Invalid_json_is_rejected(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SampleId>(json, _options));

    [Fact]
    public void An_invalid_dictionary_key_is_rejected() =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Dictionary<SampleId, int>>("""{"nope":1}""", _options));

    [Fact]
    public void The_factory_only_converts_typed_identifiers()
    {
        var factory = new TypedIdJsonConverterFactory();

        factory.CanConvert(typeof(SampleId)).ShouldBeTrue();
        factory.CanConvert(typeof(Guid)).ShouldBeFalse();
        factory.CanConvert(typeof(string)).ShouldBeFalse();
        factory.CanConvert(typeof(SampleId?)).ShouldBeFalse();
    }

    private sealed record Holder(SampleId Id, SampleId? Optional);
}
