using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_update_demographics</c>: BIAN <c>Demographics/Update</c>, a command (ADR 0024). It
/// dispatches <see cref="UpdateDemographics"/>, as <c>PUT …/Demographics/{demographicsid}/Update</c> does, with
/// <c>expectedVersion</c> in place of <c>If-Match</c>, and answers from its acknowledgement (ADR 0039, ADR 0040).
/// </summary>
[McpServerToolType]
public sealed class UpdateDemographicsTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_update_demographics";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Demographics/Update";

    /// <summary>Updates the party's Demographics, as the HTTP endpoint does: the same command, idempotency and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="demographicsId">The entry's DemographicsId.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The Demographics with the entry's new <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Update a party's demographics", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: update details about the customer's demographics. request is the BIAN "
        + "UpdateDemographics body: Socio-EconomicClassification, Employment (PartyReference, InvolvementReference), "
        + "EmploymentHistory, EducationHistory and ServicingConstraints. It is a partial update: a field left out keeps its "
        + "value, an empty one clears it, and Employment replaces the whole employment. Changed details become Unverified "
        + "until an Exchange verifies them. EthnicityReligion is accepted and never stored. Send the version you last read "
        + "as expectedVersion: one that is no longer current is a conflict. The answer's version is the new one.")]
    public static async Task<CallToolResult> UpdateAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The entry's DemographicsId, from Register or Retrieve.")] string demographicsId,
        [Description("The BIAN UpdateDemographics body, with BIAN's property names.")] UpdateDemographicsRequest request,
        [Description("The entry's version from the last Retrieve or command (the ETag on HTTP).")] long expectedVersion,
        [Description("A key of 1 to 255 visible ASCII characters, unique per update. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bus);

        var command = DemographicsIds.Read(partyReferenceDataDirectoryEntryId, demographicsId)
            .Bind(ids => IdempotencyKey.TryCreate(idempotencyKey)
                .Bind(key => request.ToCommand(ids.EntryId, ids.DemographicsId, expectedVersion, key)));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<UpdateDemographicsAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(DemographicsResult.From(acknowledged)));
    }
}
