using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Reads and writes a party identifier as its value, a plain JSON string. Applied to each identifier by attribute, so
/// it needs no registration. A value that is not a string or not a valid identifier throws <see cref="JsonException"/>.
/// </summary>
/// <typeparam name="TIdentifier">The identifier type.</typeparam>
public sealed class PartyIdentifierJsonConverter<TIdentifier> : JsonConverter<TIdentifier>
    where TIdentifier : class, IPartyIdentifier<TIdentifier>
{
    /// <inheritdoc />
    public override TIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A {typeof(TIdentifier).Name} must be a JSON string.");
        }

        var result = TIdentifier.TryCreate(reader.GetString());
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TIdentifier value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Value);
    }
}
