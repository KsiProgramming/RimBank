using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>Reads the entry and BankRelations ids of a BankRelations tool, with the errors HTTP gives for its route values.</summary>
internal static class BankRelationsIds
{
    /// <summary>The two ids, or the error of the first invalid one.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id, as sent.</param>
    /// <param name="bankRelationsId">The BankRelations id, as sent.</param>
    /// <returns>The typed ids, or the validation error.</returns>
    public static Result<(PartyReferenceDataDirectoryEntryId EntryId, BankRelationsId BankRelationsId)> Read(
        string partyReferenceDataDirectoryEntryId,
        string bankRelationsId) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId), BankRelationsId.TryCreate(bankRelationsId)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => BankRelationsErrors.BankRelationsIdInvalid,
            var (entryId, bankRelations) => (entryId.Value, bankRelations.Value),
        };
}
