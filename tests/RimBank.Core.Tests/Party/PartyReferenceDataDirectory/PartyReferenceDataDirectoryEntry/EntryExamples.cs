
namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Fixed values for the entry's tests, so every run decides the same commands.
internal static class EntryExamples
{
    public static readonly DateTimeOffset OpenDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    public static readonly PartyReferenceDataDirectoryEntryId EntryId =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate));

    public static readonly DemographicsId Demographics = DemographicsId.From(Guid.CreateVersion7(OpenDate));

    public static readonly PartyReferenceId Reference = PartyReferenceId.From(Guid.CreateVersion7(OpenDate));

    public static readonly BankRelationsId BankRelations = BankRelationsId.From(Guid.CreateVersion7(OpenDate));

    public static RegisterPartyReferenceDataDirectoryEntry Register(PartyType partyType = PartyType.Person) =>
        new(EntryId, partyType, OpenDate, Demographics, Reference, BankRelations);

    public static PartyRegistered Registered(PartyType partyType = PartyType.Person) =>
        new(EntryId, partyType, OpenDate, Demographics, Reference, BankRelations);
}
