using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Postgresql;

/// <summary>
/// How every context stores JSON: its events and documents in Marten, and the acknowledgements it remembers for
/// idempotency (ADR 0016, Serialization). Typed ids are plain GUID strings (ADR 0008) and enums are their names, so a
/// stored value never depends on the order of an enum's members.
/// </summary>
public static class StoredJson
{
    /// <summary>The options for JSON written outside Marten's own serializer, such as a remembered acknowledgement.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Adds the converters every stored value needs, except enums, which Marten configures on its own.</summary>
    /// <param name="options">Marten's or <see cref="Options"/>' serializer options.</param>
    public static void AddConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new TypedIdJsonConverterFactory());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions();
        AddConverters(options);
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
