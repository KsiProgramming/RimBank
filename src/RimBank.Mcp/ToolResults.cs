using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RimBank.Mcp;

/// <summary>The success shape of every RimBank tool (ADR 0039).</summary>
public static class ToolResults
{
    /// <summary>
    /// <paramref name="value"/> as the result's <c>structuredContent</c>, and the same JSON as its one text block for
    /// clients that read only text, as <see cref="ToolProblem"/> does for errors. It is serialized with the web defaults
    /// HTTP uses, so a tool's JSON is that of the HTTP response it mirrors.
    /// </summary>
    /// <typeparam name="T">The tool's result model.</typeparam>
    /// <param name="value">The result, built from a command's acknowledgement or a query's view.</param>
    /// <returns>The success tool result.</returns>
    public static CallToolResult Structured<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var json = JsonSerializer.Serialize(value, JsonSerializerOptions.Web);
        return new CallToolResult
        {
            IsError = false,
            StructuredContent = JsonElement.Parse(json),
            Content = [new TextContentBlock { Text = json }],
        };
    }
}
