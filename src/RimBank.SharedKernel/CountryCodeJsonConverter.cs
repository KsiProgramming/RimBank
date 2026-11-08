using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// Reads and writes a <see cref="CountryCode"/> as its code, a plain JSON string such as <c>"FR"</c>,
/// also as a dictionary key. Applied to <see cref="CountryCode"/> by attribute, so it needs no registration.
/// </summary>
/// <remarks>A value that is not a string or not a country in the table throws <see cref="JsonException"/>.</remarks>
public sealed class CountryCodeJsonConverter : JsonConverter<CountryCode>
{
    /// <inheritdoc />
    public override CountryCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? ReadText(ref reader)
            : throw new JsonException("A country must be a JSON string.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CountryCode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(CodeOf(value));
    }

    /// <inheritdoc />
    public override CountryCode ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ReadText(ref reader);

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, CountryCode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(CodeOf(value));
    }

    private static CountryCode ReadText(ref Utf8JsonReader reader)
    {
        var result = CountryCode.TryCreate(reader.GetString());
        return result.IsSuccess ? result.Value : throw new JsonException(CountryCode.InvalidCountry.Message);
    }

    private static string CodeOf(CountryCode value) =>
        value.IsDefault ? throw new JsonException("default(CountryCode) is not a country and cannot be written.") : value.Code;
}
