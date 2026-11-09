using Microsoft.Extensions.DependencyInjection;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>The Party Reference Data Directory's MCP tools, registered by its host module (ADR 0021, ADR 0039).</summary>
public static class PartyReferenceDataDirectoryTools
{
    /// <summary>Adds a tool for each implemented BIAN operation that has one (roadmap 1.16; the others in 1.17).</summary>
    /// <param name="tools">The host's MCP server builder.</param>
    /// <returns>The same builder.</returns>
    public static IMcpServerBuilder AddPartyReferenceDataDirectoryTools(this IMcpServerBuilder tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        return tools
            .WithTools<RegisterPartyTool>()
            .WithTools<RetrievePartyReferenceDataDirectoryEntryTool>()
            .WithTools<UpdatePartyReferenceDataDirectoryEntryTool>()
            .WithTools<UpdateDemographicsTool>()
            .WithTools<ExchangeDemographicsTool>()
            .WithTools<RetrieveDemographicsTool>()
            .WithTools<UpdateBankRelationsTool>()
            .WithTools<RetrieveBankRelationsTool>()
            .WithTools<RegisterPartyAssociationTool>()
            .WithTools<UpdatePartyAssociationTool>()
            .WithTools<RetrievePartyAssociationTool>();
    }
}
