using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace RimBank.Conformance.Testing;

/// <summary>One way a RimBank body is not a subset of its BIAN schema.</summary>
/// <param name="JsonPath">Where, for example <c>$.PartyReference.PartyName</c>.</param>
/// <param name="Message">What is wrong.</param>
internal sealed record SchemaProblem(string JsonPath, string Message);

/// <summary>
/// Compares the JSON schema of a RimBank type, as System.Text.Json writes it, to a BIAN schema (ADR 0037). Every
/// property RimBank writes must exist in the BIAN schema at the same path with a compatible JSON type; fewer properties
/// is always fine. The walk follows the RimBank schema, which is finite, so recursive BIAN types need no guard.
/// </summary>
internal static class SchemaSubset
{
    private const int MaxDepth = 32;

    private static readonly JsonSchemaExporterOptions _exporter = new() { TreatNullObliviousAsNonNullable = true };

    public static IReadOnlyList<SchemaProblem> Compare(Type type, JsonSerializerOptions options, BianSchema bian, StringComparison names)
    {
        var problems = new List<SchemaProblem>();
        Walk(JsonSchemaExporter.GetJsonSchemaAsNode(options, type, _exporter), bian, "$", names, problems, depth: 0);
        return problems;
    }

    private static void Walk(JsonNode? rimbank, BianSchema bian, string path, StringComparison names, List<SchemaProblem> problems, int depth)
    {
        if (depth > MaxDepth || bian.Type is null || rimbank is not JsonObject)
        {
            return;
        }

        foreach (var kind in KindsOf(rimbank))
        {
            if (Problem(kind, bian) is { } mismatch)
            {
                problems.Add(new SchemaProblem(path, mismatch));
                continue;
            }

            if (kind == "object" && rimbank?["properties"] is JsonObject properties)
            {
                foreach (var (name, property) in properties)
                {
                    var path2 = $"{path}.{name}";
                    if (bian.Property(name, names) is { } match)
                    {
                        Walk(property, match, path2, names, problems, depth + 1);
                    }
                    else
                    {
                        problems.Add(new SchemaProblem(
                            path2,
                            $"'{name}' is not a property of the BIAN schema {bian.Name}. Its properties are: {string.Join(", ", bian.PropertyNames)}."));
                    }
                }
            }
            else if (kind == "array" && bian.Items is { } items)
            {
                Walk(rimbank?["items"], items, path + "[*]", names, problems, depth + 1);
            }
        }
    }

    // The JSON types RimBank may write at this node. A nullable type also allows null, which BIAN never forbids, and a
    // number read leniently from a string (JsonSerializerDefaults.Web) is still written as a number. A schema without a
    // type (a $ref back into a recursive RimBank type), like the schema `true` of a JsonElement passthrough, allows
    // anything and is not compared.
    private static List<string> KindsOf(JsonNode? rimbank)
    {
        var kinds = rimbank?["type"] switch
        {
            JsonValue single => new List<string> { single.GetValue<string>() },
            JsonArray many => many.Select(kind => kind!.GetValue<string>()).ToList(),
            _ => new List<string>(),
        };
        kinds.Remove("null");
        if (kinds.Contains("string") && (kinds.Contains("integer") || kinds.Contains("number")))
        {
            kinds.Remove("string");
        }

        return kinds;
    }

    private static string? Problem(string kind, BianSchema bian) => (kind, bian.Type) switch
    {
        (_, _) when kind == bian.Type => null,
        ("integer", "number") => null,
        ("string", "object") when bian.IsEnumWrapper => null,
        _ => $"RimBank writes a JSON {kind} where the BIAN schema {bian.Name} declares {Article(bian.Type!)} {bian.Type}.",
    };

    private static string Article(string type) => type is ['a' or 'e' or 'i' or 'o' or 'u', ..] ? "an" : "a";
}
