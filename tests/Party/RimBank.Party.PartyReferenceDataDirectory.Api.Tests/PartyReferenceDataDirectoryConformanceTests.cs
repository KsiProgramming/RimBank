using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RimBank.Conformance.Testing;
using RimBank.Party.PartyReferenceDataDirectory.Api.Registration;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.5 and ADR 0020: every route the running host maps under /PartyReferenceDataDirectory/ is an operation of
// the BIAN specification, with its method, and every operation implemented so far is mapped. The full subset check of
// the request and response shapes is roadmap 1.14.
public sealed class PartyReferenceDataDirectoryConformanceTests(PostgresqlServer server)
{
    private static readonly string[] _implemented = [RegisterPartyEndpoint.OperationId];

    [Fact]
    public async Task The_mapped_routes_conform_to_the_BIAN_specification()
    {
        await using var host = await server.StartHostAsync();
        var specification = await BianSpecifications.LoadAsync("PartyReferenceDataDirectory");

        var routes = ImplementedRoute.From(host.Alba.Services.GetRequiredService<EndpointDataSource>());

        routes.ShouldConformTo(specification, _implemented);
    }
}
