using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>Reads the two ids every Reference route carries, entry first.</summary>
internal static class ReferenceRouteValues
{
    /// <summary>
    /// The entry and Reference ids, or <c>shared.invalid-identifier</c> naming <c>partyreferencedatadirectoryid</c> or
    /// <c>referenceid</c>.
    /// </summary>
    public static Result<(PartyReferenceDataDirectoryEntryId EntryId, PartyReferenceId PartyReferenceId)> Read(
        string partyreferencedatadirectoryid,
        string referenceid) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid), PartyReferenceId.TryCreate(referenceid)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => PartyReferenceErrors.ReferenceIdInvalid,
            var (entryId, referenceId) => (entryId.Value, referenceId.Value),
        };
}
