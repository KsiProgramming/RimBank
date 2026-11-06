using RimBank.SharedKernel;

namespace RimBank.Mcp.Errors;

/// <summary>The errors every MCP tool can return, whatever its context (ADR 0022).</summary>
public static class ToolErrors
{
    /// <summary>
    /// The call's arguments could not be bound to the tool's parameters: a value of the wrong JSON type, or a required
    /// argument missing. A 400 like an HTTP request body that cannot be read; the message never repeats the arguments.
    /// </summary>
    public static Error ArgumentsInvalid { get; } = Error.Validation(
        "shared.tool-arguments-invalid",
        "The tool's arguments do not match its input schema. Check the tool's inputSchema and call it again.");
}
