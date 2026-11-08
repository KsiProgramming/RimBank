using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The Party Reference Data Directory's read port: reads the context's views for its query handlers (tech-stack §3.4
/// and §8.2, ADR 0024). Its Marten implementation, over the inline-projected documents, lives in the context's
/// <c>.Postgresql</c> adapter. Command handlers never use it.
/// </summary>
/// <remarks>It reads the entry views and, from roadmap 1.11, the association views.</remarks>
public interface IPartyDirectoryEnquiry : IReadPort
{
    /// <summary>Finds the view of an entry.</summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry's view, or <c>null</c> if the entry does not exist.</returns>
    Task<PartyReferenceDataDirectoryEntryView?> FindEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken);

    /// <summary>Finds the view of an association (roadmap 1.11).</summary>
    /// <param name="associationId">The association.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The association's view, or <c>null</c> if the association does not exist.</returns>
    Task<PartyAssociationView?> FindAssociationAsync(PartyAssociationId associationId, CancellationToken cancellationToken);
}
