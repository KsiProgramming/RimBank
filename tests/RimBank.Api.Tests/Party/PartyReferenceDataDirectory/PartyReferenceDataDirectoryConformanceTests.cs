using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RimBank.Conformance.Testing;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.14 (ADR 0020, ADR 0037): the implemented subset of Party Reference Data Directory conforms to its BIAN v14
// OpenAPI and AsyncAPI specifications. What it implements and publishes comes from the ADR 0024 catalogue, so the
// ADR, the running host's routing table and the published language cannot drift apart. Every deliberate difference
// is a row of PartyReferenceDataDirectoryConformanceDeviations that cites its ADR.
public sealed class PartyReferenceDataDirectoryConformanceTests(PostgresqlServer server)
{
    // Wolverine's default System.Text.Json settings for messages; PartyPublishedLanguageSchemaTests proves they still
    // write the names Wolverine writes (ADR 0036).
    private static readonly JsonSerializerOptions _wire = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static Task<BianServiceDomainSpecification> Specification =>
        BianSpecifications.LoadAsync(PartyReferenceDataDirectoryCatalogue.ServiceDomain);

    [Fact]
    public async Task The_mapped_routes_conform_to_the_BIAN_specification()
    {
        await using var host = await server.StartHostAsync();

        var routes = ImplementedRoute.From(host.Alba.Services.GetRequiredService<EndpointDataSource>());

        routes.ShouldConformTo(await Specification, PartyReferenceDataDirectoryCatalogue.Implemented);
    }

    [Fact]
    public async Task The_request_and_response_bodies_and_success_statuses_conform_to_the_BIAN_specification()
    {
        await using var host = await server.StartHostAsync();
        var json = host.Alba.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        var endpoints = ImplementedEndpoint.From(host.Alba.Services.GetRequiredService<EndpointDataSource>());

        endpoints.ShouldHaveBodiesConformingTo(await Specification, json, PartyReferenceDataDirectoryConformanceDeviations.All);
    }

    [Fact]
    public async Task The_published_event_payloads_conform_to_the_BIAN_AsyncAPI_specification()
    {
        PartyReferenceDataDirectoryCatalogue.Published.ShouldHavePayloadsConformingTo(
            await Specification,
            _wire,
            PartyReferenceDataDirectoryConformanceDeviations.All);
    }
}
