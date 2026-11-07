using Microsoft.AspNetCore.Diagnostics;
using RimBank.Problems;

namespace RimBank.Host;

/// <summary>
/// Turns any exception that reaches the host into the generic <c>shared.unexpected</c> 500, in every environment. The
/// exception is logged with the trace ID; its message, type and stack trace never reach the response (ADR 0015).
/// </summary>
/// <remarks>
/// A <see cref="BadHttpRequestException"/> is the caller's fault, not the host's: the framework throws it in
/// Development when it cannot read the request (in Production it answers a bodiless status instead). It gets the
/// same answer as in Production, its own status, and a 400 is <c>shared.request-malformed</c>.
/// </remarks>
internal sealed partial class UnhandledExceptionHandler(IProblemDetailsService problemDetails, ILogger<UnhandledExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = ProblemDetailsEnrichment.TraceIdOf(httpContext);
        var (status, problem) = exception switch
        {
            BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } =>
                (StatusCodes.Status400BadRequest, ErrorProblemDetails.Create(SharedProblems.RequestMalformed)),
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = badRequest.StatusCode }),
            _ => (StatusCodes.Status500InternalServerError, ErrorProblemDetails.Create(SharedProblems.Unexpected)),
        };

        if (exception is BadHttpRequestException)
        {
            LogBadRequest(logger, httpContext.Request.Method, httpContext.Request.Path, status, traceId);
        }
        else
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path, traceId);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path} (traceId {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path, string traceId);

    // The exception's message can quote the request (a JSON fragment, a header value), so it is not logged.
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Unreadable request on {Method} {Path}: {Status} (traceId {TraceId})")]
    private static partial void LogBadRequest(ILogger logger, string method, PathString path, int status, string traceId);
}
