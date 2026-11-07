namespace RimBank.Host;

/// <summary>
/// The single entry point through which a bounded context registers itself in <c>RimBank.Host</c>
/// (specs/tech-stack.md §2, ADR 0014).
/// </summary>
/// <remarks>
/// There is one implementation per BIAN service domain, at
/// <c>RimBank.Host/Modules/&lt;BusinessDomain&gt;/&lt;ServiceDomain&gt;Module.cs</c>, listed in
/// <see cref="ServiceDomainModules.All"/>. A module only calls the <c>Add…</c> and <c>Map…</c> extension methods that
/// the context's own adapters expose (<c>.Postgresql</c>, <c>.Api</c>, <c>.Mcp</c>, <c>.Subscriptions</c>). It holds
/// no business logic and no wiring of its own, so the host composes contexts without knowing their internals.
/// </remarks>
public interface IServiceDomainModule
{
    /// <summary>The BIAN service domain name in PascalCase, for example <c>PartyReferenceDataDirectory</c>. It is unique in the host.</summary>
    string ServiceDomain { get; }

    /// <summary>Registers the context's services: its adapters' ports, stores, handlers and subscriptions.</summary>
    /// <param name="builder">The host builder, before the application is built.</param>
    void AddServices(IHostApplicationBuilder builder);

    /// <summary>
    /// Registers the context's MCP tools on the host's single MCP server (ADR 0021), by calling the one
    /// <see cref="IMcpServerBuilder"/> extension its <c>.Mcp</c> adapter exposes. The host owns the transport and the
    /// <c>/mcp</c> endpoint; a context without tools yet leaves the body empty.
    /// </summary>
    /// <param name="tools">The host's MCP server builder.</param>
    void AddMcpTools(IMcpServerBuilder tools);

    /// <summary>Maps the context's HTTP endpoints on the built application.</summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
