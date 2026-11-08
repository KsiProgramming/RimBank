using Marten;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql;

/// <summary>
/// The Marten implementation of <see cref="IPartyDirectory"/>: each entry is an event stream whose id is the entry's
/// id (ADR 0024). It works on the handler's own session, so the events commit with the handler's transaction, its
/// outbox and its idempotency outcome (ADR 0017, ADR 0018). It only maps typed ids to stream ids and holds no logic.
/// </summary>
/// <param name="session">The session Wolverine opened on the context's store for the handler.</param>
public sealed class MartenPartyDirectory(IDocumentSession session) : IPartyDirectory
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> LoadEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchStreamAsync(entryId.Value, token: cancellationToken);
        return [.. stream.Select(stored => stored.Data).OfType<PartyReferenceDataDirectoryEntryEvent>()];
    }

    /// <inheritdoc />
    public void StartEntry(PartyReferenceDataDirectoryEntryId entryId, IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        session.Events.StartStream(entryId.Value, [.. events]);
    }

    /// <inheritdoc />
    public void AppendToEntry(
        PartyReferenceDataDirectoryEntryId entryId,
        long expectedVersion,
        IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        session.Events.Append(entryId.Value, expectedVersion + events.Count, [.. events]);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PartyAssociationEvent>> LoadAssociationAsync(
        PartyAssociationId associationId,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchStreamAsync(associationId.Value, token: cancellationToken);
        return [.. stream.Select(stored => stored.Data).OfType<PartyAssociationEvent>()];
    }

    /// <inheritdoc />
    public void StartAssociation(PartyAssociationId associationId, IReadOnlyList<PartyAssociationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        session.Events.StartStream(associationId.Value, [.. events]);
    }

    /// <inheritdoc />
    public void AppendToAssociation(PartyAssociationId associationId, long expectedVersion, IReadOnlyList<PartyAssociationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        session.Events.Append(associationId.Value, expectedVersion + events.Count, [.. events]);
    }
}
