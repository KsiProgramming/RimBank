using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

/// <summary>
/// A test-only fake context: a slim application that maps the given routes with no-op handlers, read back through
/// its real routing table. No server is started and no request is sent. The fake never ships in src/.
/// </summary>
internal static class FakeContext
{
    public static async Task<IReadOnlyList<ImplementedRoute>> MapAsync(params (string Method, string Route)[] routes)
    {
        var builder = WebApplication.CreateSlimBuilder();
        await using var app = builder.Build();
        foreach (var (method, route) in routes)
        {
            app.MapMethods(route, [method], () => Results.NoContent());
        }

        return ImplementedRoute.From(app);
    }

    /// <summary>
    /// Maps endpoints with no-op handlers that declare the given request and success response types, as a context's
    /// <c>.Produces&lt;T&gt;()</c> and inferred body parameter would, and reads them back with their metadata.
    /// </summary>
    public static async Task<IReadOnlyList<ImplementedEndpoint>> MapEndpointsAsync(params FakeEndpoint[] endpoints)
    {
        var builder = WebApplication.CreateSlimBuilder();
        await using var app = builder.Build();
        foreach (var endpoint in endpoints)
        {
            var mapped = app.MapMethods(endpoint.Route, [endpoint.Method], () => Results.NoContent());
            if (endpoint.Request is { } request)
            {
                mapped.Accepts(request, "application/json");
            }

            if (endpoint.Status is { } status)
            {
                mapped.Produces(status, endpoint.Response);
            }

            mapped.ProducesProblem(StatusCodes.Status400BadRequest);
        }

        return ImplementedEndpoint.From(app);
    }
}

/// <summary>One endpoint of a fake context: its route, request body type and one declared response.</summary>
internal sealed record FakeEndpoint(string Method, string Route, Type? Request = null, int? Status = StatusCodes.Status200OK, Type? Response = null);
