using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>Reads the two ids every BankRelations route carries, entry first.</summary>
internal static class BankRelationsRouteValues
{
    /// <summary>
    /// The entry and BankRelations ids, or <c>shared.invalid-identifier</c> naming <c>partyreferencedatadirectoryid</c> or
    /// <c>bankrelationsid</c>.
    /// </summary>
    public static Result<(PartyReferenceDataDirectoryEntryId EntryId, BankRelationsId BankRelationsId)> Read(
        string partyreferencedatadirectoryid,
        string bankrelationsid) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid), BankRelationsId.TryCreate(bankrelationsid)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => BankRelationsErrors.BankRelationsIdInvalid,
            var (entryId, bankRelationsId) => (entryId.Value, bankRelationsId.Value),
        };
}
