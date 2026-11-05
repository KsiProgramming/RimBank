using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// Creates a <see cref="TypedIdJsonConverter{TId}"/> for every <see cref="ITypedId{TSelf}"/>.
/// Add it once to the <see cref="JsonSerializerOptions"/> of each adapter (Marten, Wolverine, ASP.NET Core, MCP).
/// </summary>
/// <remarks>Nullable identifiers are handled by System.Text.Json around the converter it creates.</remarks>
public sealed class TypedIdJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.IsValueType
            && Array.Exists(
                typeToConvert.GetInterfaces(),
                i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(ITypedId<>)
                    && i.GenericTypeArguments[0] == typeToConvert);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(TypedIdJsonConverter<>).MakeGenericType(typeToConvert))!;
}
