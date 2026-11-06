using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RimBank.Conformance.Testing;

/// <summary>A route a context maps: one HTTP method on one route template.</summary>
/// <param name="Method">The HTTP method, upper case.</param>
/// <param name="Route">The route template.</param>
public sealed record ImplementedRoute(string Method, RouteTemplate Route)
{
    /// <summary>
    /// Reads the routes of a running application's routing table, without sending a request: for example
    /// <c>factory.Services.GetRequiredService&lt;EndpointDataSource&gt;()</c> of a <c>WebApplicationFactory</c>.
    /// </summary>
    /// <param name="endpoints">The application's endpoint data source.</param>
    /// <returns>One route per endpoint and HTTP method. Endpoints without an HTTP method (fallbacks, hubs) are skipped.</returns>
    public static IReadOnlyList<ImplementedRoute> From(EndpointDataSource endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return From(endpoints.Endpoints);
    }

    /// <summary>
    /// Reads the routes mapped on an endpoint route builder (a <c>WebApplication</c> or a route group) before the
    /// application starts, for example after a module's <c>MapEndpoints</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>One route per endpoint and HTTP method. Endpoints without an HTTP method (fallbacks, hubs) are skipped.</returns>
    public static IReadOnlyList<ImplementedRoute> From(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return From(endpoints.DataSources.SelectMany(source => source.Endpoints));
    }

    /// <inheritdoc />
    public override string ToString() => $"{Method} {Route}";

    private static ImplementedRoute[] From(IEnumerable<Endpoint> endpoints) =>
    [
        .. endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => new ImplementedRoute(method.ToUpperInvariant(), RouteTemplate.Parse(endpoint.RoutePattern.RawText ?? string.Empty)))),
    ];
}
