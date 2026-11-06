using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using RimBank.Host.Security.Tests;

namespace RimBank.Host.Tests;

/// <summary>
/// The host as <c>Program</c> composes it, but with no service domain: what every context runs on. Since roadmap 1.4
/// <c>Program</c> also runs the Party context, which needs its database; the full host with real PostgreSQL is proven by
/// <c>RimBank.AppHost.Tests</c>.
/// </summary>
internal static class HostWithoutContexts
{
    /// <summary>Builds and starts the host in memory, with test tokens, in <paramref name="environment"/>.</summary>
    public static async Task<WebApplication> StartAsync(string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        var app = HostComposition.Build(builder, []);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
