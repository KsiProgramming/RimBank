using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RimBank.Problems;

/// <summary>Adds what every RimBank Problem Details response carries, whatever produced it (ADR 0015).</summary>
public static class ProblemDetailsEnrichment
{
    /// <summary>
    /// Sets <c>traceId</c> to the current W3C trace ID (or <see cref="HttpContext.TraceIdentifier"/> when no activity
    /// is running) and <c>instance</c> to the request path when it is not set yet.
    /// </summary>
    /// <param name="problem">The Problem Details to enrich.</param>
    /// <param name="httpContext">The current request.</param>
    public static void Apply(ProblemDetails problem, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(httpContext);

        problem.Instance ??= httpContext.Request.Path.Value;
        problem.Extensions[ProblemExtensions.TraceId] = TraceIdOf(httpContext);
    }

    /// <summary>The trace ID a caller quotes to find this request's logs and trace.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <returns>32 hex characters when a W3C activity is running; the request's trace identifier otherwise.</returns>
    public static string TraceIdOf(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity
            ? activity.TraceId.ToHexString()
            : httpContext.TraceIdentifier;
    }
}
