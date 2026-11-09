using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>Reads the entry and Reference ids of a Reference tool, with the errors HTTP gives for its route values.</summary>
internal static class ReferenceIds
{
    /// <summary>The two ids, or the error of the first invalid one.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id, as sent.</param>
    /// <param name="referenceId">The Reference id, as sent.</param>
    /// <returns>The typed ids, or the validation error.</returns>
    public static Result<(PartyReferenceDataDirectoryEntryId EntryId, PartyReferenceId PartyReferenceId)> Read(
        string partyReferenceDataDirectoryEntryId,
        string referenceId) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId), PartyReferenceId.TryCreate(referenceId)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => PartyReferenceErrors.ReferenceIdInvalid,
            var (entryId, reference) => (entryId.Value, reference.Value),
        };
}
