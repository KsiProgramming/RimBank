using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>Reads the two ids every Demographics route carries, entry first.</summary>
internal static class DemographicsRouteValues
{
    /// <summary>
    /// The entry and Demographics ids, or <c>shared.invalid-identifier</c> naming <c>partyreferencedatadirectoryid</c>
    /// or <c>demographicsid</c>.
    /// </summary>
    public static Result<(PartyReferenceDataDirectoryEntryId EntryId, DemographicsId DemographicsId)> Read(
        string partyreferencedatadirectoryid,
        string demographicsid) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid), DemographicsId.TryCreate(demographicsid)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => DemographicsErrors.DemographicsIdInvalid,
            var (entryId, demographicsId) => (entryId.Value, demographicsId.Value),
        };
}
