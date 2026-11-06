using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The Party Reference Data Directory's write port: loads and appends the event streams of its aggregates, inside the
/// command handler's transaction (tech-stack §3.4 and §8.1, ADR 0024). Its Marten implementation lives in the context's
/// <c>.Postgresql</c> adapter.
/// </summary>
/// <remarks>
/// It grows with the operations that need it: Update (1.8) adds an append with an expected version, and Associations
/// (1.11) add the <c>PartyAssociation</c> streams.
/// </remarks>
public interface IPartyDirectory : IWritePort
{
    /// <summary>Loads the events of an entry, oldest first.</summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The entry's history, empty if the entry does not exist.</returns>
    Task<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> LoadEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts the stream of a new entry with <paramref name="events"/>. The stream is only staged: the handler's
    /// transaction commits it, and fails if a stream with the same id already exists.
    /// </summary>
    /// <param name="entryId">The new entry.</param>
    /// <param name="events">The events its decision returned.</param>
    void StartEntry(PartyReferenceDataDirectoryEntryId entryId, IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events);
}
