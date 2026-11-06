using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

/// <summary>
/// An in-memory <see cref="IPartyDirectory"/> for the core's handler tests. Like Marten, it refuses to start a stream
/// that already exists. Unlike Marten, a started stream is visible at once: there is no transaction to commit.
/// </summary>
internal sealed class InMemoryPartyDirectory : IPartyDirectory
{
    private readonly Dictionary<PartyReferenceDataDirectoryEntryId, List<PartyReferenceDataDirectoryEntryEvent>> _entries = [];

    /// <summary>Every entry stream, for assertions.</summary>
    public IReadOnlyCollection<PartyReferenceDataDirectoryEntryId> Entries => _entries.Keys;

    public Task<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> LoadEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>>(StreamOf(entryId));

    public void StartEntry(PartyReferenceDataDirectoryEntryId entryId, IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (!_entries.TryAdd(entryId, [.. events]))
        {
            throw new InvalidOperationException($"The entry stream {entryId} already exists.");
        }
    }

    /// <summary>Preloads the history of <paramref name="entryId"/>, as if it had been committed earlier.</summary>
    public void Seed(PartyReferenceDataDirectoryEntryId entryId, params PartyReferenceDataDirectoryEntryEvent[] events) =>
        StartEntry(entryId, events);

    /// <summary>The events of <paramref name="entryId"/>, or none.</summary>
    public IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> StreamOf(PartyReferenceDataDirectoryEntryId entryId) =>
        _entries.TryGetValue(entryId, out var events) ? [.. events] : [];
}
