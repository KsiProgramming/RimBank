using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_retrieve_bank_relations</c>: BIAN <c>BankRelations/Retrieve</c>, a query
/// (ADR 0024). It dispatches <see cref="RetrieveBankRelations"/>, as <c>GET …/BankRelations/{bankrelationsid}/Retrieve</c>
/// does, and answers only from the entry's inline view (tech-stack §6, §8, ADR 0039).
/// </summary>
[McpServerToolType]
public sealed class RetrieveBankRelationsTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_retrieve_bank_relations";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "BankRelations/Retrieve";

    /// <summary>Retrieves the party's BankRelations, as the HTTP endpoint does: the same query and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="bankRelationsId">The entry's BankRelationsId.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The BankRelations with the entry's <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Retrieve a party's bank relations", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: retrieve details about the customer's relationship with the bank. Returns "
        + "BankRelationType, the employee or unit in BusinessUnitEmployeeReference.PartyReference, and in "
        + "BankRelationReference the RelationshipStartDate and RelationshipEndDate (UTC date-times) and the "
        + "RelationshipLifecycleStatus, with the entry's version (send it as expectedVersion to change the entry). Unknown "
        + "values are left out.")]
    public static async Task<CallToolResult> RetrieveAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The entry's BankRelationsId, from Register or Retrieve.")] string bankRelationsId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var ids = BankRelationsIds.Read(partyReferenceDataDirectoryEntryId, bankRelationsId);
        if (ids.IsFailure)
        {
            return ToolProblem.Create(ids.Error);
        }

        var view = await bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrieveBankRelations(ids.Value.EntryId, ids.Value.BankRelationsId),
            cancellationToken);
        return view.ToToolResult(found => ToolResults.Structured(BankRelationsResult.From(found)));
    }
}
