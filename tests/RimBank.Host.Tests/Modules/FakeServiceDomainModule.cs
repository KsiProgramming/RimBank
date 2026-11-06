using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RimBank.Host.Modules.Tests;

/// <summary>
/// A stand-in for a real context's module: it registers a marker service, maps <c>GET /{route}/ping</c> and records
/// each call in <paramref name="calls"/>, so the tests can check the composition order.
/// </summary>
internal sealed class FakeServiceDomainModule(string serviceDomain, string route, List<string> calls) : IServiceDomainModule
{
    public string ServiceDomain => serviceDomain;

    public void AddServices(IHostApplicationBuilder builder)
    {
        calls.Add($"{serviceDomain}.AddServices");
        builder.Services.AddKeyedSingleton(serviceDomain, new FakeModuleMarker(serviceDomain));
    }

    public void AddMcpTools(IMcpServerBuilder tools) => calls.Add($"{serviceDomain}.AddMcpTools");

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        calls.Add($"{serviceDomain}.MapEndpoints");
        endpoints.MapGet($"/{route}/ping", () => serviceDomain);
    }
}

internal sealed record FakeModuleMarker(string ServiceDomain);
