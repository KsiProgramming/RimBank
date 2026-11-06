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
using Scalar.AspNetCore;

namespace RimBank.ServiceDefaults;

/// <summary>
/// The service defaults every RimBank host applies: OpenTelemetry traces, metrics and logs, health checks, service
/// discovery, HttpClient resilience, and the OpenAPI document with its Scalar UI in Development (specs/tech-stack.md §1,
/// ADR 0014).
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>The readiness probe: every health check must pass.</summary>
    public const string HealthEndpointPath = "/health";

    /// <summary>The liveness probe: only checks tagged <see cref="LiveTag"/> must pass.</summary>
    public const string AlivenessEndpointPath = "/alive";

    /// <summary>The tag of the health checks that the liveness probe runs.</summary>
    public const string LiveTag = "live";

    /// <summary>
    /// The activity source and meter Marten reports through. Each context's store reports under it (ADR 0016); the name
    /// is a string, so the service defaults take no package reference to Marten.
    /// </summary>
    public const string MartenTelemetry = "Marten";

    /// <summary>The OpenAPI document of the host's HTTP endpoints, served in Development only.</summary>
    public const string OpenApiDocumentPath = "/openapi/v1.json";

    /// <summary>The Scalar UI over <see cref="OpenApiDocumentPath"/>, served in Development only.</summary>
    public const string ApiReferencePath = "/scalar";

    /// <summary>
    /// Adds OpenTelemetry, the default health checks, service discovery, HttpClient resilience and the OpenAPI
    /// document generator (its default document, <c>v1</c>).
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddOpenApi();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry logging, metrics (ASP.NET Core, HttpClient, runtime, Marten) and tracing (ASP.NET Core,
    /// HttpClient, Marten, and the application's own activity source). Probe requests are not traced. The OTLP exporter is used when
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
                .AddRuntimeInstrumentation()
                .AddMeter(MartenTelemetry))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(MartenTelemetry)
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
    /// <c>Degraded</c> or <c>Unhealthy</c>), never check names, exceptions or versions (ADR 0014). In Development only,
    /// it also maps the OpenAPI document (<see cref="OpenApiDocumentPath"/>) and the Scalar UI
    /// (<see cref="ApiReferencePath"/>), open to anonymous callers so a developer can read them before signing in;
    /// calling an operation from the UI still needs a token.
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

        if (endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            endpoints.MapOpenApi().AllowAnonymous();
            endpoints.MapScalarApiReference().AllowAnonymous();
        }

        return endpoints;
    }

    private static bool IsProbe(Microsoft.AspNetCore.Http.PathString path) =>
        path.StartsWithSegments(HealthEndpointPath) || path.StartsWithSegments(AlivenessEndpointPath);
}
