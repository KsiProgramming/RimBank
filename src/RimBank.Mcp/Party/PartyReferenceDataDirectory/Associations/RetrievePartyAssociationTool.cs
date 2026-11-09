using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_retrieve_associations</c>: BIAN <c>Associations/Retrieve</c>, a query (ADR 0024).
/// It dispatches <see cref="RetrievePartyAssociation"/>, as <c>GET …/Associations/{associationsid}/Retrieve</c> does,
/// and answers only from the association's inline view (tech-stack §6, §8, ADR 0039).
/// </summary>
[McpServerToolType]
public sealed class RetrievePartyAssociationTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_retrieve_associations";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Associations/Retrieve";

    /// <summary>Retrieves an association, as the HTTP endpoint does: the same query and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The id of the entry that owns the association.</param>
    /// <param name="associationsId">The association's id.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The association with its <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Retrieve a party association", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: retrieve details about an association of the customer. Returns "
        + "AssociateReference.PartyReference (the associate's entry id), AssociateType and, when recorded, "
        + "AssociationValidFromToDate in UTC, with the association's version (send it as expectedVersion to update the "
        + "association). Only the entry that owns the association finds it.")]
    public static async Task<CallToolResult> RetrieveAsync(
        [Description("The PartyReferenceDataDirectoryEntryId of the party that owns the association.")] string partyReferenceDataDirectoryEntryId,
        [Description("The AssociationsId that Register returned.")] string associationsId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var ids = AssociationIds.Read(partyReferenceDataDirectoryEntryId, associationsId);
        if (ids.IsFailure)
        {
            return ToolProblem.Create(ids.Error);
        }

        var view = await bus.InvokeAsync<Result<PartyAssociationView>>(
            new RetrievePartyAssociation(ids.Value.OwnerId, ids.Value.AssociationId),
            cancellationToken);
        return view.ToToolResult(found => ToolResults.Structured(PartyAssociationResult.From(found)));
    }
}
