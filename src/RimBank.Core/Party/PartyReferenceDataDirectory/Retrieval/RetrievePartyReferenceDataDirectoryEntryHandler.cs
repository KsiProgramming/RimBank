using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Retrieval;

/// <summary>Handles <see cref="RetrievePartyReferenceDataDirectoryEntry"/> on the read side (tech-stack §8.2).</summary>
public static class RetrievePartyReferenceDataDirectoryEntryHandler
{
    /// <summary>Reads the entry's view through the read port.</summary>
    /// <param name="query">The query.</param>
    /// <param name="enquiry">The context's read port.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry's view, or <c>party.party-not-found</c> when the entry does not exist.</returns>
    public static async Task<Result<PartyReferenceDataDirectoryEntryView>> HandleAsync(
        RetrievePartyReferenceDataDirectoryEntry query,
        IPartyDirectoryEnquiry enquiry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(enquiry);

        return await enquiry.FindEntryAsync(query.EntryId, cancellationToken) switch
        {
            { } view => view,
            null => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
        };
    }
}
