using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdatePartyReferenceDataDirectoryEntry"/> on the write side (tech-stack §8.1, ADR 0027).</summary>
public static class UpdatePartyReferenceDataDirectoryEntryHandler
{
    /// <summary>
    /// Loads the entry, checks that it exists and that the caller's expected version is its current stream version,
    /// decides Update with the current time as the refresh date, and appends the events with that expected version, so
    /// the commit also fails if another writer appended in between.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="time">The clock for the refresh date.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.party-not-found</c>, <c>party.version-conflict</c>,
    /// or the decision's <see cref="Error"/>.
    /// </returns>
    public static async Task<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>> HandleAsync(
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
            (false, _) => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (true, false) => PartyReferenceDataDirectoryEntryErrors.VersionConflict,
            (true, true) => PartyReferenceDataDirectoryEntry
                .Decide(new UpdatePartyReferenceDataDirectoryEntryDetails(command.PartyType, time.GetUtcNow()), state)
                .Map(events =>
                {
                    directory.AppendToEntry(command.EntryId, command.ExpectedVersion, events);
                    return Acknowledge(events.Aggregate(state, PartyReferenceDataDirectoryEntry.Evolve), history.Count + events.Count);
                }),
        };
    }

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
