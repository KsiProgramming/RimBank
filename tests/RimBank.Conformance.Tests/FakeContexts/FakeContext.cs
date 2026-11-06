using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RimBank.Conformance.Testing;

namespace RimBank.Conformance.FakeContexts.Tests;

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
}
