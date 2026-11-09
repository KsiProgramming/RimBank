using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_retrieve_demographics</c>: BIAN <c>Demographics/Retrieve</c>, a query (ADR 0024).
/// It dispatches <see cref="RetrieveDemographics"/>, as <c>GET …/Demographics/{demographicsid}/Retrieve</c> does, and
/// answers only from the entry's inline view (tech-stack §6, §8, ADR 0039).
/// </summary>
[McpServerToolType]
public sealed class RetrieveDemographicsTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_retrieve_demographics";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Demographics/Retrieve";

    /// <summary>Retrieves the party's Demographics, as the HTTP endpoint does: the same query and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="demographicsId">The entry's DemographicsId.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The Demographics with the entry's <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Retrieve a party's demographics", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: retrieve details about the customer's demographics. Returns the demographic "
        + "details and whether the bank has verified them, with the entry's version (send it as expectedVersion to change "
        + "the entry). Unknown details are left out, and EthnicityReligion is never returned.")]
    public static async Task<CallToolResult> RetrieveAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The entry's DemographicsId, from Register or Retrieve.")] string demographicsId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var ids = DemographicsIds.Read(partyReferenceDataDirectoryEntryId, demographicsId);
        if (ids.IsFailure)
        {
            return ToolProblem.Create(ids.Error);
        }

        var view = await bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrieveDemographics(ids.Value.EntryId, ids.Value.DemographicsId),
            cancellationToken);
        return view.ToToolResult(found => ToolResults.Structured(DemographicsResult.From(found)));
    }
}
