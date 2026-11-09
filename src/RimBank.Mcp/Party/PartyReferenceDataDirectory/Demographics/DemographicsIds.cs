using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>Reads the entry and Demographics ids of a Demographics tool, with the errors HTTP gives for its route values.</summary>
internal static class DemographicsIds
{
    /// <summary>The two ids, or the error of the first invalid one.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id, as sent.</param>
    /// <param name="demographicsId">The Demographics id, as sent.</param>
    /// <returns>The typed ids, or the validation error.</returns>
    public static Result<(PartyReferenceDataDirectoryEntryId EntryId, DemographicsId DemographicsId)> Read(
        string partyReferenceDataDirectoryEntryId,
        string demographicsId) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId), DemographicsId.TryCreate(demographicsId)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => DemographicsErrors.DemographicsIdInvalid,
            var (entryId, demographics) => (entryId.Value, demographics.Value),
        };
}
