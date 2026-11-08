using System.Diagnostics;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The acknowledgement of <see cref="RegisterPartyAssociation"/> and <see cref="UpdatePartyAssociation"/>: the association
/// as the decision left it, never a read model (tech-stack §5 and §8.1). The idempotency policy stores it and returns it
/// again for a repeated key (ADR 0018).
/// </summary>
/// <param name="AssociationId">The association.</param>
/// <param name="Version">The association's stream version after the command, which the next Update sends back as <c>If-Match</c>.</param>
/// <param name="OwnerId">The entry that owns the association.</param>
/// <param name="AssociateId">The associated entry.</param>
/// <param name="AssociateType">How the owner relates to the associate.</param>
/// <param name="ValidityPeriod">When the association holds, if known.</param>
public sealed record PartyAssociationAcknowledged(
    PartyAssociationId AssociationId,
    long Version,
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyReferenceDataDirectoryEntryId AssociateId,
    PartyRelationshipType AssociateType,
    AssociationValidityPeriod? ValidityPeriod)
{
    /// <summary>The acknowledgement of a registered association at <paramref name="version"/>.</summary>
    /// <param name="decided">The state the decision produced.</param>
    /// <param name="version">Its stream version.</param>
    /// <returns>The acknowledgement.</returns>
    internal static PartyAssociationAcknowledged From(PartyAssociation decided, long version) =>
        decided switch
        {
            { Id: { } id, OwnerId: { } owner, AssociateId: { } associate, AssociateType: { } type } =>
                new PartyAssociationAcknowledged(id, version, owner, associate, type, decided.ValidityPeriod),
            _ => throw new UnreachableException("An accepted association command leaves the association registered."),
        };
}
