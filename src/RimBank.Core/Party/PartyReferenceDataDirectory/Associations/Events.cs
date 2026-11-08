namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>A domain event of a party association (ADR 0024). Internal to this context.</summary>
public abstract record PartyAssociationEvent;

/// <summary>
/// A party was associated with another party of the directory (BIAN <c>Associations/Register</c>, roadmap 1.11). Its
/// owner and associate never change afterwards.
/// </summary>
/// <param name="AssociationId">The new association.</param>
/// <param name="OwnerId">The entry that owns the association: the path's <c>{partyreferencedatadirectoryid}</c>.</param>
/// <param name="AssociateId">The associated entry.</param>
/// <param name="AssociateType">How the owner relates to the associate.</param>
/// <param name="ValidityPeriod">When the association holds, if known.</param>
public sealed record PartyAssociationRegistered(
    PartyAssociationId AssociationId,
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyReferenceDataDirectoryEntryId AssociateId,
    PartyRelationshipType AssociateType,
    AssociationValidityPeriod? ValidityPeriod) : PartyAssociationEvent;

/// <summary>
/// An association was updated (BIAN <c>Associations/Update</c>, roadmap 1.11). It carries the type and the validity
/// period the association has from now on, changed or not.
/// </summary>
/// <param name="AssociationId">The association.</param>
/// <param name="AssociateType">How the owner relates to the associate after the update.</param>
/// <param name="ValidityPeriod">When the association holds after the update, if known.</param>
public sealed record PartyAssociationUpdated(
    PartyAssociationId AssociationId,
    PartyRelationshipType AssociateType,
    AssociationValidityPeriod? ValidityPeriod) : PartyAssociationEvent;
