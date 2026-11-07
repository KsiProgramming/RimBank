using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

// ADR 0020: the harness reads routes from the real routing table, before start-up or from a running host.
public sealed class ImplementedRouteTests
{
    [Fact]
    public async Task Routes_are_read_from_a_running_host_one_per_method()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGet("/PartyReferenceDataDirectory/{id}/Retrieve", () => Results.NoContent());
        app.MapMethods("/PartyReferenceDataDirectory/{id}/Update", ["PUT", "PATCH"], () => Results.NoContent());
        app.MapFallback(() => Results.NotFound());
        await app.StartAsync(TestContext.Current.CancellationToken);

        var routes = ImplementedRoute.From(app.Services.GetRequiredService<EndpointDataSource>());

        routes.Select(route => route.ToString()).ShouldBe(
            [
                "GET /PartyReferenceDataDirectory/{id}/Retrieve",
                "PUT /PartyReferenceDataDirectory/{id}/Update",
                "PATCH /PartyReferenceDataDirectory/{id}/Update",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Routes_are_read_from_a_route_builder_before_start_up()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGroup("/PartyReferenceDataDirectory").MapPost("/Register", () => Results.NoContent());

        ImplementedRoute.From(app).ShouldBe([new ImplementedRoute("POST", RouteTemplate.Parse("/PartyReferenceDataDirectory/Register"))]);
    }
}
