using RimBank.Party.PartyReferenceDataDirectory.Api;
using RimBank.Party.PartyReferenceDataDirectory.Postgresql;

namespace RimBank.Host;

/// <summary>
/// The Party Reference Data Directory (BIAN service domain, business domain Party): its store, inline view, and write
/// and read ports, so its command and query handlers run on the host's bus, and its HTTP endpoints (ADR 0014,
/// ADR 0024), each protected by its operation's policy (ADR 0038). Its MCP tools arrive in roadmap 1.16.
/// </summary>
public sealed class PartyReferenceDataDirectoryModule : IServiceDomainModule
{
    /// <summary>The scope a token needs for the context's queries, the <c>Retrieve</c> operations (ADR 0038).</summary>
    public const string ReadScope = "party-reference-data-directory:read";

    /// <summary>The scope a token needs for the context's commands, every operation except <c>Retrieve</c> (ADR 0038).</summary>
    public const string WriteScope = "party-reference-data-directory:write";

    /// <inheritdoc />
    public string ServiceDomain => PartyReferenceDataDirectoryPostgresql.ServiceDomain;

    /// <inheritdoc />
    public void AddServices(IHostApplicationBuilder builder) => builder.AddPartyReferenceDataDirectoryPostgresql();

    /// <inheritdoc />
    public void AddMcpTools(IMcpServerBuilder tools)
    {
    }

    /// <inheritdoc />
    public void AddOperationPolicies(OperationPolicies policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        policies
            .AddCommand(RegisterPartyEndpoint.OperationId, WriteScope)
            .AddQuery(RetrievePartyReferenceDataDirectoryEntryEndpoint.OperationId, ReadScope)
            .AddCommand(UpdatePartyReferenceDataDirectoryEntryEndpoint.OperationId, WriteScope)
            .AddCommand(UpdateDemographicsEndpoint.OperationId, WriteScope)
            .AddCommand(ExchangeDemographicsEndpoint.OperationId, WriteScope)
            .AddQuery(RetrieveDemographicsEndpoint.OperationId, ReadScope)
            .AddCommand(UpdatePartyReferenceEndpoint.OperationId, WriteScope)
            .AddQuery(RetrievePartyReferenceEndpoint.OperationId, ReadScope)
            .AddCommand(RegisterPartyAssociationEndpoint.OperationId, WriteScope)
            .AddCommand(UpdatePartyAssociationEndpoint.OperationId, WriteScope)
            .AddQuery(RetrievePartyAssociationEndpoint.OperationId, ReadScope)
            .AddCommand(UpdateBankRelationsEndpoint.OperationId, WriteScope)
            .AddQuery(RetrieveBankRelationsEndpoint.OperationId, ReadScope);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapPartyReferenceDataDirectoryEndpoints();
}
