using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RimBank.Host;
using RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

namespace RimBank.Api.Tests;

// Roadmap 1.15: the running host maps no BIAN endpoint without its operation's policy, the policy's kind matches the
// endpoint's verb (GET is a query and needs the read scope; every other verb is a command and needs the write scope),
// and no registered policy is left without its endpoint (ADR 0038).
public sealed class OperationAuthorizationCoverageTests(PostgresqlServer server)
{
    [Fact]
    public async Task Every_bian_endpoint_requires_the_policy_named_by_its_operation()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();

        var endpoints = BianEndpoints(host);

        endpoints.ShouldNotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            var name = endpoint.Metadata.GetRequiredMetadata<IEndpointNameMetadata>().EndpointName;
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ShouldBe([name], endpoint.DisplayName);
            catalog.Find(name).ShouldNotBeNull($"{name} has no policy registered by its module");
        }
    }

    [Fact]
    public async Task A_get_endpoint_is_a_query_on_the_read_scope_and_any_other_verb_a_command_on_the_write_scope()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();

        foreach (var endpoint in BianEndpoints(host))
        {
            var name = endpoint.Metadata.GetRequiredMetadata<IEndpointNameMetadata>().EndpointName;
            var policy = catalog.Find(name).ShouldNotBeNull(name);
            var methods = endpoint.Metadata.GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods;
            var isGet = methods.SequenceEqual(["GET"]);

            policy.Kind.ShouldBe(isGet ? OperationKind.Query : OperationKind.Command, name);
            policy.Scope.ShouldEndWith(isGet ? ":read" : ":write", Case.Sensitive, name);
        }
    }

    [Fact]
    public async Task Every_registered_operation_policy_protects_an_endpoint()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();

        var named = BianEndpoints(host).Select(endpoint => endpoint.Metadata.GetRequiredMetadata<IEndpointNameMetadata>().EndpointName);

        catalog.Policies.Select(policy => policy.OperationId).ShouldBe(named, ignoreOrder: true);
    }

    [Fact]
    public async Task The_party_reference_data_directory_has_eight_commands_and_five_queries()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();

        catalog.Policies.Count(policy => policy.Kind == OperationKind.Command).ShouldBe(8);
        catalog.Policies.Count(policy => policy.Kind == OperationKind.Query).ShouldBe(5);
        catalog.Scopes.ShouldBe([PartyReferenceDataDirectoryModule.WriteScope, PartyReferenceDataDirectoryModule.ReadScope], ignoreOrder: true);
    }

    // A BIAN endpoint is a named route endpoint of a module: the health probes, the MCP endpoint and the dev-only API
    // description are not BIAN operations and carry no operation name.
    private static List<RouteEndpoint> BianEndpoints(PartyApiHost host) =>
        [
            .. host.Alba.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>() is not null),
        ];
}
