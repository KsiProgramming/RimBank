using System.Reflection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;

namespace RimBank.Host;

/// <summary>
/// The host's single MCP server: Streamable HTTP at <see cref="EndpointPath"/>, stateless, with the tools each
/// context's module registers (specs/tech-stack.md §6, ADR 0021).
/// </summary>
/// <remarks>
/// The endpoint is protected by the deny-by-default fallback policy like every other endpoint (ADR 0019), so a call
/// without a valid token is a <c>shared.unauthenticated</c> Problem Details response.
/// </remarks>
public static class McpComposition
{
    /// <summary>The path of the MCP endpoint.</summary>
    public const string EndpointPath = "/mcp";

    /// <summary>The server name announced to MCP clients during initialization.</summary>
    public const string ServerName = "RimBank";

    /// <summary>The product version announced to MCP clients, without the source revision suffix.</summary>
    public static string ServerVersion { get; } = ProductVersion();

    /// <summary>
    /// Adds the MCP server with the stateless Streamable HTTP transport and the Problem Details tool errors of
    /// <c>RimBank.Mcp</c> (ADR 0022), then each module's tools in list order.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="modules">The modules to compose, the same list given to the other module steps.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddRimBankMcp<TBuilder>(this TBuilder builder, IReadOnlyList<IServiceDomainModule> modules)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddMcpServer(options => options.ServerInfo = new Implementation { Name = ServerName, Version = ServerVersion })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .AddRimBankToolErrors()
            .AddRimBankToolAuthorization()
            .AddServiceDomainMcpTools(modules);

        return builder;
    }

    /// <summary>
    /// Maps the MCP endpoint at <see cref="EndpointPath"/>, after checking that no two registered tools share a name.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// Two tools have the same name. The SDK would keep only one of them, so one context could hide another's tool.
    /// </exception>
    public static IEndpointRouteBuilder MapRimBankMcp(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var duplicates = endpoints.ServiceProvider.GetServices<McpServerTool>()
            .GroupBy(tool => tool.ProtocolTool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Each MCP tool name is registered exactly once, but these are registered more than once: {string.Join(", ", duplicates)}.");
        }

        endpoints.MapMcp(EndpointPath).RequireAuthorization(AuthenticationComposition.McpPolicy);
        return endpoints;
    }

    private static string ProductVersion()
    {
        var informational = typeof(McpComposition).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var revision = informational.IndexOf('+', StringComparison.Ordinal);
        return revision < 0 ? informational : informational[..revision];
    }
}
