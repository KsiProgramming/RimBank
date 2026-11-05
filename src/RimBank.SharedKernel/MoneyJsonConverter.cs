using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// Reads and writes <see cref="Money"/> as <c>{ "amount": "10.50", "currency": "EUR" }</c>. The amount is a
/// string, as BIAN's <c>AmountValue</c> is, so no client reads it as a floating-point number. Applied to
/// <see cref="Money"/> by attribute, so it needs no registration.
/// </summary>
/// <remarks>
/// <para>
/// Reading accepts the properties in any order and ignores unknown properties (tolerant reader). It keeps any
/// scale, because stored values (events, views) may hold unrounded amounts; the syntax is the same strict
/// invariant format as <see cref="Money.TryCreate"/>. Client input goes through <see cref="Money.TryCreate"/>
/// in the adapters, which also enforces the currency's minor units.
/// </para>
/// <para>A missing or invalid amount or currency, or a non-string amount, throws <see cref="JsonException"/>.</para>
/// </remarks>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    private const int AnyScale = int.MaxValue;

    /// <inheritdoc />
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Money must be a JSON object with an amount and a currency.");
        }

        string? amount = null;
        string? currency = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var property = reader.GetString();
            reader.Read();
            (amount, currency) = property switch
            {
                Money.AmountField => (StringValue(ref reader, "The amount must be a JSON string, for example \"10.50\"."), currency),
                Money.CurrencyField => (amount, StringValue(ref reader, "The currency must be a JSON string, for example \"EUR\".")),
                _ => Skipped(ref reader, amount, currency),
            };
        }

        var result = Currency.TryCreate(currency);
        if (result.IsFailure)
        {
            throw new JsonException(result.Error.Message);
        }

        return Money.TryParseAmount(amount, AnyScale, out var value)
            ? Money.Of(value, result.Value)
            : throw new JsonException("The amount must be a number with a dot as decimal separator, for example \"10.50\".");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.Currency.IsDefault)
        {
            throw new JsonException("default(Money) is not money and cannot be written.");
        }

        writer.WriteStartObject();
        writer.WriteString(Money.AmountField, value.FormatAmount());
        writer.WriteString(Money.CurrencyField, value.Currency.Code);
        writer.WriteEndObject();
    }

    // An unknown property is skipped (tolerant reader); what was read so far is kept.
    private static (string? Amount, string? Currency) Skipped(ref Utf8JsonReader reader, string? amount, string? currency)
    {
        reader.Skip();
        return (amount, currency);
    }

    private static string? StringValue(ref Utf8JsonReader reader, string message) =>
        reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException(message);
}
