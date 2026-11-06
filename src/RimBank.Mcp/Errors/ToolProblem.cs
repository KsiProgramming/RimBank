using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Protocol;
using RimBank.Problems;
using RimBank.SharedKernel;

namespace RimBank.Mcp.Errors;

/// <summary>
/// An <see cref="Error"/> as an MCP tool result: <c>isError: true</c>, with the RFC 9457 Problem Details that HTTP
/// returns for the same error as <c>structuredContent</c>, and the same JSON as the one text block of <c>content</c>
/// for clients that read only text (specs/tech-stack.md §6, ADR 0021, ADR 0022).
/// </summary>
public static class ToolProblem
{
    /// <summary>
    /// Builds the error tool result for <paramref name="error"/> with the mapping of <c>RimBank.Problems</c>, so its
    /// <c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>errorCode</c> and <c>errors</c> are those of HTTP.
    /// </summary>
    /// <param name="error">The business, validation, not-found or conflict error.</param>
    /// <param name="httpContext">
    /// The <c>/mcp</c> request, which adds <c>instance</c> and <c>traceId</c> as on HTTP. Tools leave it out: the
    /// call-tool filter of <see cref="McpServerBuilderExtensions.AddRimBankToolErrors"/> adds both to every error result.
    /// </param>
    /// <returns>The error tool result.</returns>
    public static CallToolResult Create(Error error, HttpContext? httpContext = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var problem = ErrorProblemDetails.Create(error);
        if (httpContext is not null)
        {
            ProblemDetailsEnrichment.Apply(problem, httpContext);
        }

        return Of(problem);
    }

    /// <summary>Whether <paramref name="result"/> is an error result that carries a RimBank Problem Details.</summary>
    /// <param name="result">A tool result.</param>
    /// <returns><c>true</c> when it is <c>isError</c> with an <c>errorCode</c> in its structured content.</returns>
    public static bool IsProblem(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsError == true
            && result.StructuredContent is { ValueKind: JsonValueKind.Object } content
            && content.TryGetProperty(ProblemExtensions.ErrorCode, out _);
    }

    /// <summary>
    /// Adds the request's <c>instance</c> (when the problem has none) and <c>traceId</c> to a problem result, as the
    /// HTTP pipeline does for every Problem Details response (<see cref="ProblemDetailsEnrichment"/>).
    /// </summary>
    /// <param name="result">A result for which <see cref="IsProblem"/> is <c>true</c>.</param>
    /// <param name="httpContext">The <c>/mcp</c> request.</param>
    /// <returns>A new result with the enriched Problem Details in both <c>structuredContent</c> and <c>content</c>.</returns>
    public static CallToolResult WithRequest(CallToolResult result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        if (!IsProblem(result))
        {
            throw new ArgumentException("Only an error result that carries a Problem Details can be enriched.", nameof(result));
        }

        // Read back into the same Problem Details type and enrich it as HTTP does, so the JSON stays byte for byte the
        // one HTTP writes for the same error (same members, same order).
        var content = result.StructuredContent!.Value;
        ProblemDetails problem = content.TryGetProperty("errors", out _)
            ? content.Deserialize<HttpValidationProblemDetails>(JsonSerializerOptions.Web)!
            : content.Deserialize<ProblemDetails>(JsonSerializerOptions.Web)!;
        ProblemDetailsEnrichment.Apply(problem, httpContext);
        return Of(problem);
    }

    private static CallToolResult Of(ProblemDetails problem)
    {
        var json = JsonSerializer.Serialize(problem, problem.GetType(), JsonSerializerOptions.Web);
        return new CallToolResult
        {
            IsError = true,
            StructuredContent = JsonElement.Parse(json),
            Content = [new TextContentBlock { Text = json }],
        };
    }
}
