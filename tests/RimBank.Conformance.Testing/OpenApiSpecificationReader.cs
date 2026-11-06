using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace RimBank.Conformance.Testing;

/// <summary>Reads the operations of a BIAN ext-DDD OpenAPI (YAML) specification with Microsoft.OpenApi (ADR 0020).</summary>
internal static class OpenApiSpecificationReader
{
    public static async Task<IReadOnlyList<BianOperation>> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();

        await using var stream = file.OpenRead();
        var result = await OpenApiDocument.LoadAsync(stream, "yaml", settings, cancellationToken);
        var errors = result.Diagnostic?.Errors ?? [];
        if (result.Document is not { } document || errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"The OpenAPI specification '{file.FullName}' could not be read:{Environment.NewLine}"
                + string.Join(Environment.NewLine, errors.Select(error => $"- {error.Pointer}: {error.Message}")));
        }

        return
        [
            .. (document.Paths ?? [])
                .SelectMany(path => (path.Value.Operations ?? [])
                    .Select(operation => new BianOperation(
                        operation.Value.OperationId ?? $"{operation.Key} {path.Key}",
                        operation.Key.Method.ToUpperInvariant(),
                        RouteTemplate.Parse(path.Key)))),
        ];
    }
}
