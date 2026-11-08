namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Fixed values for the association's tests (roadmap 1.11), so every run decides the same commands. The owner is the
// entry of EntryExamples.
internal static class AssociationExamples
{
    public static readonly DateTimeOffset RegisteredAt = EntryExamples.OpenDate.AddDays(10);

    public static readonly PartyReferenceDataDirectoryEntryId Owner = EntryExamples.EntryId;

    public static readonly PartyReferenceDataDirectoryEntryId Associate =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(EntryExamples.OpenDate.AddMinutes(1)));

    public static readonly PartyReferenceDataDirectoryEntryId Stranger =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(EntryExamples.OpenDate.AddMinutes(2)));

    public static readonly PartyAssociationId AssociationId = PartyAssociationId.From(Guid.CreateVersion7(RegisteredAt));

    public static readonly AssociationValidityPeriod Period =
        new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), null);

    public static readonly AssociationValidityPeriod EndedPeriod =
        new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

    public static PartyAssociationTerms Terms(
        PartyReferenceDataDirectoryEntryId? associate = null,
        PartyRelationshipType type = PartyRelationshipType.PartyIsParentOfParty,
        AssociationValidityPeriod? period = null) =>
        new(associate ?? Associate, type, period ?? Period);

    public static RegisterPartyAssociationDetails Register(
        PartyAssociationTerms? terms = null,
        bool ownerIsRegistered = true,
        bool associateIsRegistered = true) =>
        new(AssociationId, Owner, terms ?? Terms(), ownerIsRegistered, associateIsRegistered);

    public static PartyAssociationRegistered Registered(
        PartyRelationshipType type = PartyRelationshipType.PartyIsParentOfParty,
        AssociationValidityPeriod? period = null) =>
        new(AssociationId, Owner, Associate, type, period ?? Period);

    public static UpdatePartyAssociationDetails Update(
        PartyAssociationChanges? changes = null,
        PartyReferenceDataDirectoryEntryId? owner = null) =>
        new(owner ?? Owner, changes ?? PartyAssociationChanges.None);

    public static PartyAssociationUpdated Updated(
        PartyRelationshipType type = PartyRelationshipType.PartyIsParentOfParty,
        AssociationValidityPeriod? period = null) =>
        new(AssociationId, type, period);
}
