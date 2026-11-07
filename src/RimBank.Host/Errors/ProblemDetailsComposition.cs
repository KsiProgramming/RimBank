using Microsoft.AspNetCore.Diagnostics;
using RimBank.Problems;

namespace RimBank.Host;

/// <summary>Makes every non-2xx response of the host RFC 9457 Problem Details (ADR 0015).</summary>
public static class ProblemDetailsComposition
{
    /// <summary>
    /// Registers the Problem Details service, which adds <c>traceId</c> and <c>instance</c> to every Problem Details
    /// response, and the handler that turns unhandled exceptions into the generic 500.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddRimBankProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => ProblemDetailsEnrichment.Apply(context.ProblemDetails, context.HttpContext));
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Adds the exception handler and status-code pages to the pipeline, before any endpoint. A bodiless 400 (the
    /// framework could not bind the request) becomes <c>shared.request-malformed</c>, a bodiless 404 (no endpoint
    /// matched) <c>shared.not-found</c>, and the bodiless 401 and 403 of authentication <c>shared.unauthenticated</c>
    /// and <c>shared.forbidden</c> (ADR 0019); any other bodiless 4xx/5xx gets a Problem Details body with the
    /// framework's default type.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseRimBankProblemDetails(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(WriteStatusCodeProblemAsync);

        return app;
    }

    private static async Task WriteStatusCodeProblemAsync(StatusCodeContext context)
    {
        var httpContext = context.HttpContext;
        var problem = httpContext.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => ErrorProblemDetails.Create(SharedProblems.RequestMalformed),
            StatusCodes.Status401Unauthorized => ErrorProblemDetails.Create(SharedProblems.Unauthenticated),
            StatusCodes.Status403Forbidden => ErrorProblemDetails.Create(SharedProblems.Forbidden),
            StatusCodes.Status404NotFound => ErrorProblemDetails.Create(SharedProblems.NotFound),
            var status => new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = status },
        };

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }
}
