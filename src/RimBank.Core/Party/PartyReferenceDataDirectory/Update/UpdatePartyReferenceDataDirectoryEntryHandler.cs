using System.Diagnostics;
using RimBank.SharedKernel;
using Published = RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdatePartyReferenceDataDirectoryEntry"/> on the write side (tech-stack §8.1, ADR 0027).</summary>
public static class UpdatePartyReferenceDataDirectoryEntryHandler
{
    /// <summary>
    /// Loads the entry, checks that it exists and that the caller's expected version is its current stream version,
    /// decides Update with the current time as the refresh date, and appends the events with that expected version, so
    /// the commit also fails if another writer appended in between. An accepted update also publishes
    /// <c>PartyReferenceDataDirectoryEntry/Updated</c>, mapped from the decided event and cascaded into the outbox in
    /// the same transaction (ADR 0036).
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="time">The clock for the refresh date.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state with the integration event to publish; or
    /// <c>party.party-not-found</c>, <c>party.version-conflict</c> or the decision's <see cref="Error"/>, with nothing
    /// to publish.
    /// </returns>
    public static async Task<(Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged> Outcome, Published.PartyReferenceDataDirectoryEntryUpdated? Updated)> HandleAsync(
        UpdatePartyReferenceDataDirectoryEntry command,
        IPartyDirectory directory,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(time);

        var history = await directory.LoadEntryAsync(command.EntryId, cancellationToken);
        var state = history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        return (state.IsRegistered, history.Count == command.ExpectedVersion) switch
        {
            (false, _) => (PartyReferenceDataDirectoryEntryErrors.PartyNotFound, null),
            (true, false) => (PartyReferenceDataDirectoryEntryErrors.VersionConflict, null),
            (true, true) => PartyReferenceDataDirectoryEntry
                .Decide(new UpdatePartyReferenceDataDirectoryEntryDetails(command.PartyType, time.GetUtcNow()), state)
                .Match<(Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>, Published.PartyReferenceDataDirectoryEntryUpdated?)>(
                    events =>
                    {
                        directory.AppendToEntry(command.EntryId, command.ExpectedVersion, events);
                        return (
                            Acknowledge(events.Aggregate(state, PartyReferenceDataDirectoryEntry.Evolve), history.Count + events.Count),
                            Publish(events, history.Count, time));
                    },
                    error => (error, null)),
        };
    }

    private static Published.PartyReferenceDataDirectoryEntryUpdated? Publish(
        IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events,
        long versionBefore,
        TimeProvider time) =>
        events
            .Select((domainEvent, index) => domainEvent is PartyReferenceDataDirectoryEntryUpdated updated
                ? PartyReferenceDataDirectoryPublication.Updated(updated, versionBefore + index + 1, Guid.CreateVersion7(time.GetUtcNow()))
                : null)
            .SingleOrDefault(published => published is not null);

    private static UpdatePartyReferenceDataDirectoryEntryAcknowledged Acknowledge(PartyReferenceDataDirectoryEntry updated, long version) =>
        updated switch
        {
            {
                Id: { } entryId,
                PartyType: { } partyType,
                OpenDate: { } openDate,
                RefreshDate: { } refreshDate,
                DemographicsId: { } demographicsId,
                PartyReferenceId: { } partyReferenceId,
                BankRelationsId: { } bankRelationsId,
            } => new UpdatePartyReferenceDataDirectoryEntryAcknowledged(
                entryId, version, partyType, openDate, refreshDate, demographicsId, partyReferenceId, bankRelationsId),
            _ => throw new UnreachableException("An accepted Update leaves the entry registered and refreshed."),
        };
}
