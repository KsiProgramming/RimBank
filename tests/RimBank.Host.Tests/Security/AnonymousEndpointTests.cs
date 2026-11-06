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

        AnonymousRoutes(app).ShouldBe([ServiceDefaultsExtensions.HealthEndpointPath, ServiceDefaultsExtensions.AlivenessEndpointPath], ignoreOrder: true);
    }

    // Roadmap 1.5: in Development a developer can also read the OpenAPI document and the Scalar UI before signing in.
    [Fact]
    public async Task In_Development_the_API_description_is_also_open()
    {
        await using var app = await StartAsync("Development");

        // Scalar also serves its own script and icon files under /scalar/.
        var anonymous = AnonymousRoutes(app);
        anonymous.Where(route => route?.StartsWith(ServiceDefaultsExtensions.ApiReferencePath + "/", StringComparison.Ordinal) != true).ShouldBe(
        [
            ServiceDefaultsExtensions.HealthEndpointPath,
            ServiceDefaultsExtensions.AlivenessEndpointPath,
            "/openapi/{documentName}.json",
        ],
        ignoreOrder: true);
        anonymous.ShouldContain("/scalar/{documentName?}");
    }

    [Fact]
    public async Task Every_module_endpoint_is_mapped_and_protected()
    {
        await using var app = await StartAsync();

        var moduleEndpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } route && (route.StartsWith("/secured/", StringComparison.Ordinal) || route.StartsWith("/faulty/", StringComparison.Ordinal)))
            .ToList();

        moduleEndpoints.Count.ShouldBe(7);
        moduleEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    private static List<string?> AnonymousRoutes(WebApplication app) =>
        [
            .. app.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                .Select(endpoint => endpoint.RoutePattern.RawText),
        ];

    private static async Task<WebApplication> StartAsync(string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        var app = HostComposition.Build(builder, [new SecuredServiceDomainModule(), new FaultyServiceDomainModule()]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
