using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_register_associations</c>: BIAN <c>Associations/Register</c>, a command that starts
/// a new association, its own aggregate (ADR 0024, ADR 0033). It dispatches <see cref="RegisterPartyAssociation"/>, as
/// <c>POST …/Associations/Register</c> does, and answers from its acknowledgement (ADR 0039, ADR 0040). It changes no
/// existing record, so it takes no <c>expectedVersion</c>.
/// </summary>
[McpServerToolType]
public sealed class RegisterPartyAssociationTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_register_associations";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Associations/Register";

    /// <summary>Associates the party with another party, as the HTTP endpoint does: the same command, idempotency and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The id of the entry that owns the association.</param>
    /// <param name="request">The BIAN Register body.</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The new association with its <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Associate a party with another party", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: register a new association of the customer with another party of the "
        + "directory. In request, AssociateReference.PartyReference (the associate's PartyReferenceDataDirectoryEntryId) "
        + "and AssociateType are required. AssociateType is one of PartyIsParentOfParty, PartyIsSubsidiaryOfParty, "
        + "PartyIsCertifiedByParty, PartyIsMarriedToParty, PartyActsOnBehalfOfParty, PartyIsContactPointForParty, "
        + "PartyIsTrustedByParty, PartyIsThePowerOfAttorneyForParty or PartyReceivesBenefitsFromAParty, read as \"this "
        + "party <type> the associate\". AssociationValidFromToDate (FromDateTime and ToDateTime, ISO 8601, from not after "
        + "to) is optional. The associate must be registered and must not be the party itself. The other BIAN Associations "
        + "fields are refused with party.association-field-not-supported. Returns the AssociationsId and the "
        + "association's version (send it as expectedVersion to update the association). Calling again with the same "
        + "idempotencyKey and arguments returns the same association.")]
    public static async Task<CallToolResult> RegisterAsync(
        [Description("The PartyReferenceDataDirectoryEntryId of the party that owns the association.")] string partyReferenceDataDirectoryEntryId,
        [Description("The BIAN RegisterAssociations body, with BIAN's property names.")] PartyAssociationRequest request,
        [Description("A key of 1 to 255 visible ASCII characters, unique per registration. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bus);

        var command = AssociationIds.ReadOwner(partyReferenceDataDirectoryEntryId)
            .Bind(ownerId => IdempotencyKey.TryCreate(idempotencyKey)
                .Bind(key => request.ToRegisterCommand(ownerId, key)));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<PartyAssociationAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(PartyAssociationResult.From(acknowledged)));
    }
}
