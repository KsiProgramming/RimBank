using RimBank.Party.PartyReferenceDataDirectory.Postgresql;

namespace RimBank.Host.Modules.Party;

/// <summary>
/// The Party Reference Data Directory (BIAN service domain, business domain Party): its store and write port, so its
/// command handlers run on the host's bus (ADR 0014, ADR 0024). Its HTTP endpoints arrive in roadmap 1.5 and its MCP
/// tools in 1.16.
/// </summary>
public sealed class PartyReferenceDataDirectoryModule : IServiceDomainModule
{
    /// <inheritdoc />
    public string ServiceDomain => PartyReferenceDataDirectoryPostgresql.ServiceDomain;

    /// <inheritdoc />
    public void AddServices(IHostApplicationBuilder builder) => builder.AddPartyReferenceDataDirectoryPostgresql();

    /// <inheritdoc />
    public void AddMcpTools(IMcpServerBuilder tools)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
