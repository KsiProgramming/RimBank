using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RimBank.Host.Errors.Tests;
using RimBank.ServiceDefaults;

namespace RimBank.Host.Security.Tests;

// Roadmap 0.13: deny by default. Whatever a module maps, only the health probes are open to anonymous callers; the
// architecture rule allow-anonymous-only-in-service-defaults guards the source, and this test guards the running
// host (ADR 0019).
public sealed class AnonymousEndpointTests
{
    [Fact]
    public async Task Only_the_health_probes_allow_anonymous_callers()
    {
        await using var app = await StartAsync();

        var anonymous = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        anonymous.ShouldBe([ServiceDefaultsExtensions.HealthEndpointPath, ServiceDefaultsExtensions.AlivenessEndpointPath], ignoreOrder: true);
    }

    [Fact]
    public async Task Every_module_endpoint_is_mapped_and_protected()
    {
        await using var app = await StartAsync();

        var moduleEndpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } route && (route.StartsWith("/secured/", StringComparison.Ordinal) || route.StartsWith("/faulty/", StringComparison.Ordinal)))
            .ToList();

        moduleEndpoints.Count.ShouldBe(6);
        moduleEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        var app = HostComposition.Build(builder, [new SecuredServiceDomainModule(), new FaultyServiceDomainModule()]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
