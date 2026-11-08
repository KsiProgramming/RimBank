using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Reads and writes a Reference text value (an identifier or a name) as its text, a plain JSON string. Applied to each
/// value type by attribute, so it needs no registration. A value that is not a string or not valid throws <see cref="JsonException"/>.
/// </summary>
/// <typeparam name="TText">The value type.</typeparam>
public sealed class PartyReferenceTextJsonConverter<TText> : JsonConverter<TText>
    where TText : class, IPartyReferenceText<TText>
{
    /// <inheritdoc />
    public override TText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A {typeof(TText).Name} must be a JSON string.");
        }

        var result = TText.TryCreate(reader.GetString());
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TText value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Value);
    }
}
