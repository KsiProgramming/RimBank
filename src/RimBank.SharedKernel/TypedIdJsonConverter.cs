using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// Reads and writes a <typeparamref name="TId"/> as a plain JSON string in the lower-case <c>D</c> format,
/// also as a dictionary key. Usually registered for every identifier through
/// <see cref="TypedIdJsonConverterFactory"/>.
/// </summary>
/// <remarks>An invalid value (not a string, not a GUID, empty or not version 7) throws <see cref="JsonException"/>.</remarks>
public sealed class TypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, ITypedId<TId>
{
    /// <inheritdoc />
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? ReadText(ref reader)
            : throw new JsonException("An identifier must be a JSON string.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc />
    public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ReadText(ref reader);

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(TypedId.Format(value.Value));
    }

    private static TId ReadText(ref Utf8JsonReader reader) =>
        TypedId.TryParse(reader.GetString().AsSpan(), out TId id)
            ? id
            : throw new JsonException("The value is not a valid identifier.");
}
