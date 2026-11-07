using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Retrieval;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The Party Reference Data Directory's read port: reads the context's views for its query handlers (tech-stack §3.4
/// and §8.2, ADR 0024). Its Marten implementation, over the inline-projected documents, lives in the context's
/// <c>.Postgresql</c> adapter. Command handlers never use it.
/// </summary>
/// <remarks>It grows with the queries that need it: Associations (1.11) add the association view.</remarks>
public interface IPartyDirectoryEnquiry : IReadPort
{
    /// <summary>Finds the view of an entry.</summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry's view, or <c>null</c> if the entry does not exist.</returns>
    Task<PartyReferenceDataDirectoryEntryView?> FindEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken);
}
