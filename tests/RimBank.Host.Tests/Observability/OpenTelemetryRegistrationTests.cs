using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RimBank.ServiceDefaults;

namespace RimBank.Host.Observability.Tests;

// OpenTelemetry traces and metrics are part of every host (mission quality bar, tech-stack §1).
public sealed class OpenTelemetryRegistrationTests
{
    [Fact]
    public async Task Host_registers_a_tracer_provider_and_a_meter_provider()
    {
        await using var factory = new WebApplicationFactory<Program>();

        factory.Services.GetService<TracerProvider>().ShouldNotBeNull();
        factory.Services.GetService<MeterProvider>().ShouldNotBeNull();
    }

    [Fact]
    public async Task Host_traces_the_activities_every_context_store_reports_through_Marten()
    {
        await using var factory = new WebApplicationFactory<Program>();
        _ = factory.Services.GetRequiredService<TracerProvider>();
        using var marten = new ActivitySource(ServiceDefaultsExtensions.MartenTelemetry);

        marten.HasListeners().ShouldBeTrue();
    }
}
