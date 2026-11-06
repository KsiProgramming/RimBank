using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// Reads and writes an <see cref="IdempotencyKey"/> as a plain JSON string. Applied to <see cref="IdempotencyKey"/> by
/// attribute, so it needs no registration.
/// </summary>
/// <remarks>A value that is not a string or not a valid key throws <see cref="JsonException"/>.</remarks>
public sealed class IdempotencyKeyJsonConverter : JsonConverter<IdempotencyKey>
{
    /// <inheritdoc />
    public override IdempotencyKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("An idempotency key must be a JSON string.");
        }

        var result = IdempotencyKey.TryCreate(reader.GetString());
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IdempotencyKey value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.IsDefault
            ? throw new JsonException("default(IdempotencyKey) is not a key and cannot be written.")
            : value.Value);
    }
}
