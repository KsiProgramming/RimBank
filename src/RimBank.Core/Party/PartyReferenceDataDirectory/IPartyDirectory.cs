using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The Party Reference Data Directory's write port: loads and appends the event streams of its aggregates, inside the
/// command handler's transaction (tech-stack §3.4 and §8.1, ADR 0024). Its Marten implementation lives in the context's
/// <c>.Postgresql</c> adapter.
/// </summary>
/// <remarks>
/// It grows with the operations that need it: Associations (1.11) add the <c>PartyAssociation</c> streams.
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

    /// <summary>
    /// Appends <paramref name="events"/> to an existing entry whose stream is at <paramref name="expectedVersion"/>
    /// (ADR 0027). The events are only staged: the handler's transaction commits them, and fails if another writer
    /// appended to the stream in the meantime.
    /// </summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="expectedVersion">The stream version the handler decided against.</param>
    /// <param name="events">The events its decision returned.</param>
    void AppendToEntry(
        PartyReferenceDataDirectoryEntryId entryId,
        long expectedVersion,
        IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events);
}
