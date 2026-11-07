using RimBank.Party.PartyReferenceDataDirectory.Api;
using RimBank.Party.PartyReferenceDataDirectory.Postgresql;

namespace RimBank.Host;

/// <summary>
/// The Party Reference Data Directory (BIAN service domain, business domain Party): its store, inline view, and write
/// and read ports, so its command and query handlers run on the host's bus, and its HTTP endpoints (ADR 0014,
/// ADR 0024). Its MCP tools arrive in roadmap 1.16.
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
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapPartyReferenceDataDirectoryEndpoints();
}
