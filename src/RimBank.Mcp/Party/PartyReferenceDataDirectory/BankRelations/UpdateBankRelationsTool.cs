using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_update_bank_relations</c>: BIAN <c>BankRelations/Update</c>, a command (ADR 0024,
/// ADR 0035). It dispatches <see cref="UpdateBankRelations"/>, as <c>PUT …/BankRelations/{bankrelationsid}/Update</c>
/// does, with <c>expectedVersion</c> in place of <c>If-Match</c>, and answers from its acknowledgement (ADR 0039,
/// ADR 0040).
/// </summary>
[McpServerToolType]
public sealed class UpdateBankRelationsTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_update_bank_relations";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "BankRelations/Update";

    /// <summary>Updates the party's BankRelations, as the HTTP endpoint does: the same command, idempotency and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="bankRelationsId">The entry's BankRelationsId.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The BankRelations with the entry's new <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Update a party's bank relations", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: update details about the customer's relationship with the bank. request is "
        + "the BIAN UpdateBankRelations body, a partial update. BankRelationType is RelationshipManager or AccountOfficer "
        + "(BusinessUnitEmployeeReference.PartyReference then names a registered person) or ServicingBranch or "
        + "ServicingBusinessUnit (it then names a registered organisation); the reference must be another entry of the "
        + "directory and needs a type. BankRelationReference records RelationshipStartDate and RelationshipEndDate "
        + "(DateTimeContent, an ISO 8601 date or date-time, the end not before the start) and "
        + "RelationshipLifecycleStatus.StatusDescription (Active, Suspended or Ended). A value left out keeps its value, an "
        + "empty one clears it, and an empty BankRelationReference clears the dates and the status. Every other BIAN part "
        + "is refused with party.bank-relations-field-not-supported. Send the version you last read as expectedVersion: one "
        + "that is no longer current is a conflict. The answer's version is the new one.")]
    public static async Task<CallToolResult> UpdateAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The entry's BankRelationsId, from Register or Retrieve.")] string bankRelationsId,
        [Description("The BIAN UpdateBankRelations body, with BIAN's property names.")] UpdateBankRelationsRequest request,
        [Description("The entry's version from the last Retrieve or command (the ETag on HTTP).")] long expectedVersion,
        [Description("A key of 1 to 255 visible ASCII characters, unique per update. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bus);

        var command = BankRelationsIds.Read(partyReferenceDataDirectoryEntryId, bankRelationsId)
            .Bind(ids => IdempotencyKey.TryCreate(idempotencyKey)
                .Bind(key => request.ToCommand(ids.EntryId, ids.BankRelationsId, expectedVersion, key)));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<UpdateBankRelationsAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(BankRelationsResult.From(acknowledged)));
    }
}
