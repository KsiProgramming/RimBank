using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_update_associations</c>: BIAN <c>Associations/Update</c>, a command (ADR 0024,
/// ADR 0033). It dispatches <see cref="UpdatePartyAssociation"/>, as <c>PUT …/Associations/{associationsid}/Update</c>
/// does, with the association's <c>expectedVersion</c> in place of <c>If-Match</c>, and answers from its acknowledgement
/// (ADR 0039, ADR 0040).
/// </summary>
[McpServerToolType]
public sealed class UpdatePartyAssociationTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_update_associations";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Associations/Update";

    /// <summary>Updates an association, as the HTTP endpoint does: the same command, idempotency and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The id of the entry that owns the association.</param>
    /// <param name="associationsId">The association's id.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="expectedVersion">The association's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The association with its new <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Update a party association", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: update details about an association of the customer. request is the BIAN "
        + "UpdateAssociations body, a partial update of AssociateType and AssociationValidFromToDate (FromDateTime and "
        + "ToDateTime, ISO 8601, from not after to). A value left out keeps its value, and an empty validity period clears "
        + "it; the type cannot be cleared. The owner and the associate never change: an AssociateReference.PartyReference "
        + "other than the registered associate is refused with party.associate-cannot-change. Send the association's "
        + "version you last read as expectedVersion (it is the association's own version, not the entry's): one that is no "
        + "longer current is a conflict. The answer's version is the new one.")]
    public static async Task<CallToolResult> UpdateAsync(
        [Description("The PartyReferenceDataDirectoryEntryId of the party that owns the association.")] string partyReferenceDataDirectoryEntryId,
        [Description("The AssociationsId that Register returned.")] string associationsId,
        [Description("The BIAN UpdateAssociations body, with BIAN's property names.")] PartyAssociationRequest request,
        [Description("The association's version from its last Retrieve or command (the ETag on HTTP).")] long expectedVersion,
        [Description("A key of 1 to 255 visible ASCII characters, unique per update. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bus);

        var command = AssociationIds.Read(partyReferenceDataDirectoryEntryId, associationsId)
            .Bind(ids => IdempotencyKey.TryCreate(idempotencyKey)
                .Bind(key => request.ToUpdateCommand(ids.OwnerId, ids.AssociationId, expectedVersion, key)));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<PartyAssociationAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(PartyAssociationResult.From(acknowledged)));
    }
}
