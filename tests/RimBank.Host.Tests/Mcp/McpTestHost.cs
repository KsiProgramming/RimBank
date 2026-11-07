using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Client;

namespace RimBank.Host.Tests;

/// <summary>Starts the host in memory with test tokens, and connects the SDK's own MCP client to its <c>/mcp</c>.</summary>
internal static class McpTestHost
{
    public static async Task<WebApplication> StartAsync(params IServiceDomainModule[] modules)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        var app = HostComposition.Build(builder, modules);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>Connects with a valid test token, optionally sending a W3C <c>traceparent</c> on every request.</summary>
    public static Task<McpClient> ConnectAsync(WebApplication app, string? traceParent = null)
    {
        var http = TestTokens.Authenticated(app.GetTestClient());
        if (traceParent is not null)
        {
            http.DefaultRequestHeaders.Add("traceparent", traceParent);
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpComposition.EndpointPath),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            ownsHttpClient: true);
        return McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }
}
