using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A party association, the Associations Behavior Qualifier as its own aggregate (ADR 0024): the fold of its event
/// stream. Every member is <c>null</c> until the association is registered.
/// </summary>
/// <param name="Id">The association.</param>
/// <param name="OwnerId">The entry that owns the association.</param>
/// <param name="AssociateId">The associated entry.</param>
/// <param name="AssociateType">How the owner relates to the associate.</param>
/// <param name="ValidityPeriod">When the association holds, if known.</param>
public sealed partial record PartyAssociation(
    PartyAssociationId? Id,
    PartyReferenceDataDirectoryEntryId? OwnerId,
    PartyReferenceDataDirectoryEntryId? AssociateId,
    PartyRelationshipType? AssociateType,
    AssociationValidityPeriod? ValidityPeriod)
    : IDecider<PartyAssociation, PartyAssociationCommand, PartyAssociationEvent>
{
    /// <summary>An association that is not registered yet.</summary>
    public static PartyAssociation Initial { get; } = new(null, null, null, null, null);

    /// <summary>Whether the association has been registered.</summary>
    public bool IsRegistered => Id is not null;

    /// <inheritdoc />
    public static PartyAssociation Evolve(PartyAssociation state, PartyAssociationEvent domainEvent) =>
        domainEvent switch
        {
            PartyAssociationRegistered registered => new PartyAssociation(
                registered.AssociationId,
                registered.OwnerId,
                registered.AssociateId,
                registered.AssociateType,
                registered.ValidityPeriod),
            PartyAssociationUpdated updated => state with
            {
                AssociateType = updated.AssociateType,
                ValidityPeriod = updated.ValidityPeriod,
            },
            _ => throw new UnreachableException($"The association does not know {domainEvent}."),
        };
}
