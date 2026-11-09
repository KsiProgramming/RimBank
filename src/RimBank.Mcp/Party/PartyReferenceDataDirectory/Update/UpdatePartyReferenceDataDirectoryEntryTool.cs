using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_update</c>: BIAN <c>PartyReferenceDataDirectoryEntry/Update</c>, a command
/// (ADR 0024). It dispatches <see cref="UpdatePartyReferenceDataDirectoryEntry"/>, as
/// <c>PUT /PartyReferenceDataDirectory/{id}/Update</c> does, with <c>expectedVersion</c> in place of <c>If-Match</c>, and
/// answers from its acknowledgement, never from a read model (tech-stack §6, ADR 0039, ADR 0040).
/// </summary>
[McpServerToolType]
public sealed class UpdatePartyReferenceDataDirectoryEntryTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_update";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "PartyReferenceDataDirectoryEntry/Update";

    /// <summary>Updates a directory entry, as the HTTP endpoint does: the same command, idempotency and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The updated entry with its new <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Update a party directory entry", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: update details about the customer's directory entry, and record the time of "
        + "the update as its RefreshDate. In request, PartyType (Person or Organisation) is optional: leave it out to keep "
        + "the current one; PartyReference and the directory entry date fields are accepted and ignored. Send the version "
        + "you last read as expectedVersion: one that is no longer current is a conflict, so retrieve the entry and retry. "
        + "The answer's version is the new one.")]
    public static async Task<CallToolResult> UpdateAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The BIAN UpdatePartyReferenceDataDirectoryEntry body, with BIAN's property names.")] UpdatePartyReferenceDataDirectoryEntryRequest request,
        [Description("The entry's version from the last Retrieve or command (the ETag on HTTP).")] long expectedVersion,
        [Description("A key of 1 to 255 visible ASCII characters, unique per update. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bus);

        var entryId = PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId);
        if (entryId.IsFailure)
        {
            return ToolProblem.Create(PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid);
        }

        var command = IdempotencyKey.TryCreate(idempotencyKey)
            .Bind(key => request.ToCommand(entryId.Value, expectedVersion, key));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(UpdatePartyReferenceDataDirectoryEntryResult.From(acknowledged)));
    }
}
