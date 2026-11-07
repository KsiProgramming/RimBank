using ModelContextProtocol.Protocol;
using RimBank.SharedKernel;
using Result = RimBank.SharedKernel.Result;

namespace RimBank.Mcp;

/// <summary>
/// Turns the <see cref="Result{T}"/> of a command or a query into a tool result. A <c>.Mcp</c> adapter returns its
/// failures this way and never throws for them (ADR 0022).
/// </summary>
public static class ResultToolExtensions
{
    /// <summary>
    /// A success becomes the tool result <paramref name="onSuccess"/> builds; a failure becomes
    /// <see cref="ToolProblem.Create"/>.
    /// </summary>
    /// <typeparam name="T">The acknowledgement or view.</typeparam>
    /// <param name="result">The outcome the message bus returned.</param>
    /// <param name="onSuccess">Builds the tool result of a success, for example from the <c>.Api</c> response model.</param>
    /// <returns>The tool result.</returns>
    public static CallToolResult ToToolResult<T>(this Result<T> result, Func<T, CallToolResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : ToolProblem.Create(result.Error);
    }

    /// <summary>
    /// A success becomes the tool result <paramref name="onSuccess"/> builds; a failure becomes
    /// <see cref="ToolProblem.Create"/>.
    /// </summary>
    /// <param name="result">The outcome the message bus returned.</param>
    /// <param name="onSuccess">Builds the tool result of a success.</param>
    /// <returns>The tool result.</returns>
    public static CallToolResult ToToolResult(this Result result, Func<CallToolResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess() : ToolProblem.Create(result.Error);
    }
}
