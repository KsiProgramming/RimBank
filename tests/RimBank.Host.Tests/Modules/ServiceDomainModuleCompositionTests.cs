using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace RimBank.Host.Modules.Tests;

// The module entry-point convention (tech-stack §2, ADR 0014), proven with fake modules: no real context exists yet.
public sealed class ServiceDomainModuleCompositionTests
{
    private readonly List<string> _calls = [];

    [Fact]
    public async Task Every_module_registers_its_services_and_maps_its_endpoints()
    {
        await using var app = await StartAsync(
            new FakeServiceDomainModule("SampleLedger", "sample-ledger", _calls),
            new FakeServiceDomainModule("SampleStatement", "sample-statement", _calls));
        using var client = app.GetTestClient();

        app.Services.GetRequiredKeyedService<FakeModuleMarker>("SampleLedger").ServiceDomain.ShouldBe("SampleLedger");
        app.Services.GetRequiredKeyedService<FakeModuleMarker>("SampleStatement").ServiceDomain.ShouldBe("SampleStatement");
        (await client.GetStringAsync(new Uri("/sample-ledger/ping", UriKind.Relative), TestContext.Current.CancellationToken)).ShouldBe("SampleLedger");
        (await client.GetStringAsync(new Uri("/sample-statement/ping", UriKind.Relative), TestContext.Current.CancellationToken)).ShouldBe("SampleStatement");
    }

    [Fact]
    public async Task Modules_are_registered_then_mapped_in_list_order()
    {
        await using var app = await StartAsync(
            new FakeServiceDomainModule("SampleStatement", "sample-statement", _calls),
            new FakeServiceDomainModule("SampleLedger", "sample-ledger", _calls));

        _calls.ShouldBe(
        [
            "SampleStatement.AddServices",
            "SampleLedger.AddServices",
            "SampleStatement.MapEndpoints",
            "SampleLedger.MapEndpoints",
        ]);
    }

    [Fact]
    public void A_service_domain_registered_twice_stops_the_host_from_starting()
    {
        var exception = Should.Throw<InvalidOperationException>(() => HostComposition.Build(
            NewBuilder(),
            [
                new FakeServiceDomainModule("SampleLedger", "sample-ledger", _calls),
                new FakeServiceDomainModule("SampleLedger", "sample-ledger-again", _calls),
            ]));

        exception.Message.ShouldContain("SampleLedger");
        _calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Host_starts_and_is_healthy_with_no_modules()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void The_host_runs_no_service_domain_until_stage_1()
    {
        ServiceDomainModules.All.ShouldBeEmpty();
    }

    private static WebApplicationBuilder NewBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        return builder;
    }

    private static async Task<WebApplication> StartAsync(params IServiceDomainModule[] modules)
    {
        var app = HostComposition.Build(NewBuilder(), modules);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
