using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace RimBank.Conformance.Testing;

/// <summary>
/// A BIAN YAML specification read as a JSON tree, so OpenAPI and AsyncAPI schemas are walked the same way (ADR 0037).
/// Scalars are kept as strings: the harness reads only <c>type</c>, <c>properties</c>, <c>items</c> and <c>$ref</c>.
/// A key repeated in one mapping keeps its last value. Local <c>$ref</c>s (<c>#/components/…</c>) are resolved on demand.
/// </summary>
internal sealed class SpecificationDocument
{
    private const int MaxReferenceChain = 32;

    private SpecificationDocument(FileInfo file, JsonNode root)
    {
        File = file;
        Root = root;
    }

    public FileInfo File { get; }

    public JsonNode Root { get; }

    public static async Task<SpecificationDocument> LoadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var yaml = new YamlStream();
        using (var reader = file.OpenText())
        {
            yaml.Load(new StringReader(await reader.ReadToEndAsync(cancellationToken)));
        }

        var root = yaml.Documents.FirstOrDefault()?.RootNode is { } node
            ? ToJson(node) ?? new JsonObject()
            : new JsonObject();
        return new SpecificationDocument(file, root);
    }

    /// <summary>Follows <c>$ref</c> until a node without one, or <see langword="null"/> when a reference cannot be resolved.</summary>
    public JsonNode? Resolve(JsonNode? node)
    {
        for (var hops = 0; hops < MaxReferenceChain; hops++)
        {
            if (node is not JsonObject mapping || mapping["$ref"] is not JsonValue reference)
            {
                return node;
            }

            node = At(reference.GetValue<string>());
        }

        throw new InvalidOperationException($"A $ref chain in '{File.FullName}' is longer than {MaxReferenceChain} hops.");
    }

    /// <summary>The node at a local JSON pointer such as <c>#/components/schemas/Party</c>, or <see langword="null"/>.</summary>
    public JsonNode? At(string pointer)
    {
        if (!pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        JsonNode? node = Root;
        foreach (var segment in pointer[2..].Split('/'))
        {
            var key = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node is JsonObject mapping ? mapping[key] : null;
        }

        return node;
    }

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode mapping => ToJson(mapping),
        YamlSequenceNode sequence => new JsonArray([.. sequence.Children.Select(ToJson)]),
        YamlScalarNode { Value: { } value } => JsonValue.Create(value),
        _ => null,
    };

    private static JsonObject ToJson(YamlMappingNode mapping)
    {
        var json = new JsonObject();
        foreach (var (key, value) in mapping.Children)
        {
            if (key is YamlScalarNode { Value: { } name })
            {
                json[name] = ToJson(value);
            }
        }

        return json;
    }
}
