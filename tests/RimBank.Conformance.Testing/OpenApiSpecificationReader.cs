using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace RimBank.Conformance.Testing;

/// <summary>
/// Reads the operations of a BIAN ext-DDD OpenAPI (YAML) specification with Microsoft.OpenApi (ADR 0020), and their
/// request and success response body schemas from the same file read as a JSON tree (ADR 0037).
/// </summary>
internal static class OpenApiSpecificationReader
{
    private const string ErrorSchema = "HTTPError";

    public static async Task<IReadOnlyList<BianOperation>> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();

        OpenApiDocument document;
        await using (var stream = file.OpenRead())
        {
            var result = await OpenApiDocument.LoadAsync(stream, "yaml", settings, cancellationToken);
            var errors = result.Diagnostic?.Errors ?? [];
            if (result.Document is not { } loaded || errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The OpenAPI specification '{file.FullName}' could not be read:{Environment.NewLine}"
                    + string.Join(Environment.NewLine, errors.Select(error => $"- {error.Pointer}: {error.Message}")));
            }

            document = loaded;
        }

        var tree = await SpecificationDocument.LoadAsync(file, cancellationToken);
        return
        [
            .. (document.Paths ?? [])
                .SelectMany(path => (path.Value.Operations ?? [])
                    .Select(operation => Operation(
                        tree,
                        path.Key,
                        operation.Key.Method,
                        operation.Value.OperationId ?? $"{operation.Key} {path.Key}"))),
        ];
    }

    private static BianOperation Operation(SpecificationDocument tree, string path, string method, string operationId)
    {
        var node = tree.Root["paths"]?[path]?[method.ToLowerInvariant()] as JsonObject;
        var operation = new BianOperation(operationId, method.ToUpperInvariant(), RouteTemplate.Parse(path));
        return operation with
        {
            RequestBody = BodyOf(tree, node?["requestBody"], operationId + " request"),
            SuccessResponses = SuccessResponsesOf(tree, node?["responses"] as JsonObject, operation),
        };
    }

    private static Dictionary<int, BianSchema?> SuccessResponsesOf(SpecificationDocument tree, JsonObject? responses, BianOperation operation)
    {
        var success = new Dictionary<int, BianSchema?>();
        foreach (var (code, response) in responses ?? new JsonObject())
        {
            if (int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var status) && status is >= 200 and < 300)
            {
                var body = BodyOf(tree, response, $"{operation.OperationId} {status}");
                success[status] = body?.Name == ErrorSchema ? OwnResponseOf(tree, operation) : body;
            }
        }

        return success;
    }

    // BIAN v14 wires the 200 of some Control Record operations to the BadRequest response (an HTTPError body), while
    // their own response component, named <Action><Record>Response like every other operation's, is left unused.
    private static BianSchema? OwnResponseOf(SpecificationDocument tree, BianOperation operation)
    {
        var record = operation.OperationId[..operation.OperationId.IndexOf('/', StringComparison.Ordinal)];
        var name = $"{operation.Action}{record}Response";
        return tree.At("#/components/responses/" + name) is { } own
            ? BodyOf(tree, own, name)
            : null;
    }

    // A request body or response: its content's JSON media type (BIAN writes the key as 'application/json:'), or the
    // first media type when none is JSON.
    private static BianSchema? BodyOf(SpecificationDocument tree, JsonNode? bodyOrReference, string name)
    {
        if (tree.Resolve(bodyOrReference)?["content"] is not JsonObject content || content.Count == 0)
        {
            return null;
        }

        var media = content.FirstOrDefault(entry => entry.Key.StartsWith("application/json", StringComparison.OrdinalIgnoreCase));
        var schema = (media.Key is null ? content.First().Value : media.Value)?["schema"];
        return BianSchema.From(tree, schema, name);
    }
}
