using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Problems;

namespace RimBank.Mcp.Errors;

/// <summary>
/// The call-tool filter of every RimBank MCP server (ADR 0022). It runs around each tool call and:
/// <list type="bullet">
/// <item>adds the request's <c>instance</c> and <c>traceId</c> to a tool's Problem Details result;</item>
/// <item>turns arguments the SDK cannot bind to the tool's parameters into <see cref="ToolErrors.ArgumentsInvalid"/>
/// (400), without repeating them;</item>
/// <item>turns an exception into <c>shared.unexpected</c>, with no exception message or type, and logs it at Error
/// with the <c>traceId</c>, as the HTTP exception handler does.</item>
/// </list>
/// A cancelled call and an <see cref="McpProtocolException"/> pass through: the SDK answers them as the MCP
/// specification requires.
/// </summary>
public static partial class ToolErrorFilter
{
    /// <summary>The log category of unexpected tool failures.</summary>
    public const string LogCategory = "RimBank.Mcp.ToolErrors";

    /// <summary>Wraps the next call-tool handler.</summary>
    /// <param name="next">The next handler (the tool, or the next filter).</param>
    /// <returns>The wrapping handler.</returns>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Wrap(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return async (context, cancellationToken) =>
        {
            var httpContext = context.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
            CallToolResult result;
            try
            {
                result = await next(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (McpProtocolException)
            {
                throw;
            }
            catch (Exception exception) when (IsArgumentBindingFailure(exception))
            {
                return httpContext is null
                    ? ToolProblem.Create(ToolErrors.ArgumentsInvalid)
                    : ToolProblem.Create(ToolErrors.ArgumentsInvalid, httpContext);
            }
            catch (Exception exception)
            {
                var problem = httpContext is null
                    ? ToolProblem.Create(SharedProblems.Unexpected)
                    : ToolProblem.Create(SharedProblems.Unexpected, httpContext);
                LogUnexpected(
                    LoggerOf(context.Services),
                    exception,
                    context.Params?.Name ?? "unknown",
                    httpContext is null ? "none" : ProblemDetailsEnrichment.TraceIdOf(httpContext));
                return problem;
            }

            return httpContext is not null && ToolProblem.IsProblem(result) ? ToolProblem.WithRequest(result, httpContext) : result;
        };
    }

    /// <summary>
    /// Whether the SDK failed to bind the call's arguments to the tool's parameters before the tool ran (pinned by
    /// tests, ADR 0022): a value of the wrong JSON type is a <see cref="System.Text.Json.JsonException"/>, and a missing
    /// required argument an <see cref="ArgumentException"/> for the parameter <c>arguments</c>. Tools receive typed
    /// values and never parse JSON themselves.
    /// </summary>
    /// <param name="exception">The exception the call threw.</param>
    /// <returns><c>true</c> when the arguments, not the tool, are at fault.</returns>
    public static bool IsArgumentBindingFailure(Exception exception) =>
        exception is System.Text.Json.JsonException or ArgumentException { ParamName: "arguments" };

    private static ILogger LoggerOf(IServiceProvider? services) =>
        services?.GetService<ILoggerFactory>()?.CreateLogger(LogCategory) ?? NullLogger.Instance;

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception in MCP tool {ToolName} (traceId {TraceId})")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string toolName, string traceId);
}
