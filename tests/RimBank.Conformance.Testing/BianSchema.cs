using System.Text.Json.Nodes;

namespace RimBank.Conformance.Testing;

/// <summary>
/// A JSON schema of a BIAN specification: the body of an operation or the payload of a channel (ADR 0037). Its
/// <c>$ref</c>s are resolved within its own document as the schema is walked, so recursive ISO 20022 types are fine.
/// </summary>
public sealed class BianSchema
{
    private readonly SpecificationDocument _document;
    private readonly JsonObject _node;

    private BianSchema(SpecificationDocument document, JsonObject node, string name)
    {
        _document = document;
        _node = node;
        Name = name;
    }

    /// <summary>The schema's name: the last segment of its <c>$ref</c>, for example <c>RegisterPartyReferenceDataDirectoryEntry</c>.</summary>
    public string Name { get; }

    /// <summary>The JSON type the schema declares (<c>object</c>, <c>array</c>, <c>string</c>, …), <c>object</c> when it only lists properties, or <see langword="null"/> when it allows anything.</summary>
    public string? Type =>
        _node["type"] is JsonValue type ? type.GetValue<string>()
        : _node["properties"] is JsonObject ? "object"
        : null;

    /// <summary>The names of its properties, in specification order.</summary>
    public IReadOnlyList<string> PropertyNames =>
        _node["properties"] is JsonObject properties ? [.. properties.Select(property => property.Key)] : [];

    /// <summary>The schema of its array items, or <see langword="null"/>.</summary>
    public BianSchema? Items => From(_document, _node["items"], Name + "[]");

    /// <summary>
    /// Whether it is one of BIAN's generated enum wrappers: an object with a single string property, such as
    /// <c>Partytypevalues: { Partytypevalues: Person | Organisation }</c>. RimBank flattens these to plain strings (ADR 0024).
    /// </summary>
    public bool IsEnumWrapper =>
        Type == "object" && PropertyNames is [var only] && Property(only, StringComparison.Ordinal)?.Type == "string";

    /// <summary>The schema of one of its properties, or <see langword="null"/> when it has no such property.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="comparison">How names are compared: exact for HTTP bodies, ignoring case for event payloads.</param>
    public BianSchema? Property(string name, StringComparison comparison) =>
        _node["properties"] is JsonObject properties
        && properties.FirstOrDefault(property => string.Equals(property.Key, name, comparison)) is { Key: not null } match
            ? From(_document, match.Value, match.Key)
            : null;

    /// <inheritdoc />
    public override string ToString() => Name;

    internal static BianSchema? From(SpecificationDocument document, JsonNode? node, string fallbackName)
    {
        var name = node is JsonObject { } mapping && mapping["$ref"] is JsonValue reference
            ? reference.GetValue<string>()[(reference.GetValue<string>().LastIndexOf('/') + 1)..]
            : fallbackName;
        return document.Resolve(node) is JsonObject resolved ? new BianSchema(document, resolved, name) : null;
    }
}
