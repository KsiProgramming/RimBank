using Microsoft.AspNetCore.Diagnostics;
using RimBank.Problems;

namespace RimBank.Host.Errors;

/// <summary>
/// Turns any exception that reaches the host into the generic <c>shared.unexpected</c> 500, in every environment. The
/// exception is logged with the trace ID; its message, type and stack trace never reach the response (ADR 0015).
/// </summary>
internal sealed partial class UnhandledExceptionHandler(IProblemDetailsService problemDetails, ILogger<UnhandledExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path, ProblemDetailsEnrichment.TraceIdOf(httpContext));

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ErrorProblemDetails.Create(SharedProblems.Unexpected),
        });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path} (traceId {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path, string traceId);
}
