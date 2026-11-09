using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_retrieve</c>: BIAN <c>PartyReferenceDataDirectoryEntry/Retrieve</c>, a query
/// (ADR 0024). It dispatches <see cref="RetrievePartyReferenceDataDirectoryEntry"/>, as
/// <c>GET /PartyReferenceDataDirectory/{id}/Retrieve</c> does, and answers only from the entry's view (tech-stack §5,
/// §6, ADR 0039).
/// </summary>
[McpServerToolType]
public sealed class RetrievePartyReferenceDataDirectoryEntryTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_retrieve";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "PartyReferenceDataDirectoryEntry/Retrieve";

    /// <summary>Retrieves a directory entry, as the HTTP endpoint does: the same query and the same errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id, as Register returned it.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The entry with its <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Retrieve a party directory entry", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: retrieve details about a customer directory entry. Returns the party type, "
        + "the open date, the last refresh date if any, the ids of its Demographics, Reference and BankRelations, and the "
        + "entry's version (send it as expectedVersion to change the entry).")]
    public static async Task<CallToolResult> RetrieveAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var entryId = PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId);
        if (entryId.IsFailure)
        {
            return ToolProblem.Create(PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid);
        }

        var view = await bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrievePartyReferenceDataDirectoryEntry(entryId.Value),
            cancellationToken);
        return view.ToToolResult(found => ToolResults.Structured(RetrievePartyReferenceDataDirectoryEntryResult.From(found)));
    }
}
