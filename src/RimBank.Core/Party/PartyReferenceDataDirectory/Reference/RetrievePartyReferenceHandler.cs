using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="RetrievePartyReference"/> on the read side (tech-stack §8.2, ADR 0024).</summary>
public static class RetrievePartyReferenceHandler
{
    /// <summary>Reads the entry's view through the read port and checks that the Reference id is the entry's.</summary>
    /// <param name="query">The query.</param>
    /// <param name="enquiry">The context's read port.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The entry's view, <c>party.party-not-found</c> when the entry does not exist, or
    /// <c>party.behavior-qualifier-not-found</c> when the Reference id is not the entry's.
    /// </returns>
    public static async Task<Result<PartyReferenceDataDirectoryEntryView>> HandleAsync(
        RetrievePartyReference query,
        IPartyDirectoryEnquiry enquiry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(enquiry);

        return await enquiry.FindEntryAsync(query.EntryId, cancellationToken) switch
        {
            null => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            { } view when view.PartyReferenceId != query.PartyReferenceId => PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            { } view => view,
        };
    }
}
