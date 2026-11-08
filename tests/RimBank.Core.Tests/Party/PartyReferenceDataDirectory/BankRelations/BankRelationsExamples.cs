using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Fixed values for the BankRelations tests (roadmap 1.12): a bank employee (a person) and a branch (an organisation),
// registered at the same time as the entry, and the relationship the entry records with them.
internal static class BankRelationsExamples
{
    public static readonly PartyReferenceDataDirectoryEntryId ManagerId =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate.AddMinutes(3)));

    public static readonly PartyReferenceDataDirectoryEntryId BranchId =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate.AddMinutes(4)));

    public static readonly DateTimeOffset StartDate = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset EndDate = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

    public static readonly BankRelationsDetails ManagedRelation = new(
        BankRelationType.RelationshipManager,
        ManagerId,
        StartDate,
        null,
        BankRelationLifecycleStatus.Active);

    public static UpdateBankRelationsDetails UpdateRelations(BankRelationsChanges changes, PartyType? referencedPartyType = null) =>
        new(BankRelations, changes, referencedPartyType);

    public static BankRelationsUpdated RelationsUpdated(BankRelationsDetails details) =>
        new(EntryId, BankRelations, details);
}
