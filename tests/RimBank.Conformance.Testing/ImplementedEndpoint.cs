using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace RimBank.Conformance.Testing;

/// <summary>A response an endpoint declares: its status code and its body type, <see langword="null"/> when it has no body.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="Type">The CLR type of its JSON body, or <see langword="null"/>.</param>
public sealed record ImplementedResponse(int StatusCode, Type? Type);

/// <summary>
/// An endpoint a context maps, with the body types it declares (ADR 0037): the request type minimal APIs infer from its
/// body parameter (<see cref="IAcceptsMetadata"/>) and the responses of its <c>.Produces&lt;T&gt;()</c> calls
/// (<see cref="IProducesResponseTypeMetadata"/>).
/// </summary>
/// <param name="Route">Its method and route template.</param>
/// <param name="RequestType">The CLR type of its JSON request body, or <see langword="null"/> when it reads none.</param>
/// <param name="Responses">The responses it declares.</param>
public sealed record ImplementedEndpoint(ImplementedRoute Route, Type? RequestType, IReadOnlyList<ImplementedResponse> Responses)
{
    /// <summary>The 2xx responses it declares.</summary>
    public IEnumerable<ImplementedResponse> SuccessResponses => Responses.Where(response => response.StatusCode is >= 200 and < 300);

    /// <summary>Reads the endpoints of a running application's routing table, without sending a request.</summary>
    /// <param name="endpoints">The application's endpoint data source.</param>
    /// <returns>One endpoint per route endpoint and HTTP method. Endpoints without an HTTP method are skipped.</returns>
    public static IReadOnlyList<ImplementedEndpoint> From(EndpointDataSource endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return From(endpoints.Endpoints);
    }

    /// <summary>Reads the endpoints mapped on an endpoint route builder before the application starts.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>One endpoint per route endpoint and HTTP method. Endpoints without an HTTP method are skipped.</returns>
    public static IReadOnlyList<ImplementedEndpoint> From(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return From(endpoints.DataSources.SelectMany(source => source.Endpoints));
    }

    /// <inheritdoc />
    public override string ToString() => Route.ToString();

    private static ImplementedEndpoint[] From(IEnumerable<Endpoint> endpoints) =>
    [
        .. endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => new ImplementedEndpoint(
                    new ImplementedRoute(method.ToUpperInvariant(), RouteTemplate.Parse(endpoint.RoutePattern.RawText ?? string.Empty)),
                    endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType,
                    [
                        .. endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
                            .Select(response => new ImplementedResponse(
                                response.StatusCode,
                                response.Type is null || response.Type == typeof(void) ? null : response.Type)),
                    ]))),
    ];
}
