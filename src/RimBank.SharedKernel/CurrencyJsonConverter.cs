using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// Reads and writes a <see cref="Currency"/> as its code, a plain JSON string such as <c>"EUR"</c>,
/// also as a dictionary key. Applied to <see cref="Currency"/> by attribute, so it needs no registration.
/// </summary>
/// <remarks>A value that is not a string or not a currency in the table throws <see cref="JsonException"/>.</remarks>
public sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    /// <inheritdoc />
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? ReadText(ref reader)
            : throw new JsonException("A currency must be a JSON string.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(CodeOf(value));
    }

    /// <inheritdoc />
    public override Currency ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ReadText(ref reader);

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(CodeOf(value));
    }

    private static Currency ReadText(ref Utf8JsonReader reader)
    {
        var result = Currency.TryCreate(reader.GetString());
        return result.IsSuccess ? result.Value : throw new JsonException(Currency.InvalidCurrency.Message);
    }

    private static string CodeOf(Currency value) =>
        value.IsDefault ? throw new JsonException("default(Currency) is not a currency and cannot be written.") : value.Code;
}
