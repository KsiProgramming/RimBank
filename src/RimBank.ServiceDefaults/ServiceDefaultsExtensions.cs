using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace RimBank.ServiceDefaults;

/// <summary>
/// The service defaults every RimBank host applies: OpenTelemetry traces, metrics and logs, health checks, service
/// discovery and HttpClient resilience (specs/tech-stack.md §1, ADR 0014).
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>The readiness probe: every health check must pass.</summary>
    public const string HealthEndpointPath = "/health";

    /// <summary>The liveness probe: only checks tagged <see cref="LiveTag"/> must pass.</summary>
    public const string AlivenessEndpointPath = "/alive";

    /// <summary>The tag of the health checks that the liveness probe runs.</summary>
    public const string LiveTag = "live";

    /// <summary>Adds OpenTelemetry, the default health checks, service discovery and HttpClient resilience.</summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry logging, metrics (ASP.NET Core, HttpClient, runtime) and tracing (ASP.NET Core, HttpClient,
    /// and the application's own activity source). Probe requests are not traced. The OTLP exporter is used when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, as Aspire does locally.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsProbe(context.Request.Path))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>Adds the <c>self</c> health check, tagged <see cref="LiveTag"/>, so the liveness probe has a check to run.</summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    /// <summary>
    /// Maps <see cref="HealthEndpointPath"/> and <see cref="AlivenessEndpointPath"/> in every environment, open to
    /// anonymous callers so orchestrators can probe them. The body is the status only (<c>Healthy</c>,
    /// <c>Degraded</c> or <c>Unhealthy</c>), never check names, exceptions or versions (ADR 0014).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(HealthEndpointPath).AllowAnonymous();
        endpoints.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
        }).AllowAnonymous();

        return endpoints;
    }

    private static bool IsProbe(Microsoft.AspNetCore.Http.PathString path) =>
        path.StartsWithSegments(HealthEndpointPath) || path.StartsWithSegments(AlivenessEndpointPath);
}
