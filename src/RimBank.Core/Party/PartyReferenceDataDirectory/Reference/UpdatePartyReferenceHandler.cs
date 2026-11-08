using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdatePartyReference"/> on the write side (tech-stack §8.1, ADR 0024, ADR 0027).</summary>
public static class UpdatePartyReferenceHandler
{
    /// <summary>
    /// Loads the entry; checks that it exists, that the Reference id is its own and that the caller's expected version
    /// is current; loads each other entry the changes reference through the write port, never a view (roadmap 1.21,
    /// ADR 0034); decides the Update and appends it with that expected version.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="time">The clock for today's date, against which a date of birth is checked.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.party-not-found</c>,
    /// <c>party.behavior-qualifier-not-found</c>, <c>party.version-conflict</c> or the decision's error (including
    /// <c>party.referenced-party-not-registered</c> and <c>party.referenced-party-type-mismatch</c>).
    /// </returns>
    public static async Task<Result<UpdatePartyReferenceAcknowledged>> HandleAsync(
        UpdatePartyReference command,
        IPartyDirectory directory,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(time);

        var history = await directory.LoadEntryAsync(command.EntryId, cancellationToken);
        var state = history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        var referencedParties = state.IsRegistered && history.Count == command.ExpectedVersion
            ? await LoadReferencedPartiesAsync(directory, command, cancellationToken)
            : null;

        return (state.IsRegistered, state.PartyReferenceId == command.PartyReferenceId, history.Count == command.ExpectedVersion) switch
        {
            (false, _, _) => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (true, false, _) => PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (true, true, false) => PartyReferenceDataDirectoryEntryErrors.VersionConflict,
            (true, true, true) => PartyReferenceDataDirectoryEntry
                .Decide(
                    new UpdatePartyReferenceDetails(
                        command.PartyReferenceId,
                        command.Changes,
                        DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime),
                        referencedParties),
                    state)
                .Map(events =>
                {
                    directory.AppendToEntry(command.EntryId, command.ExpectedVersion, events);
                    var decided = events.Aggregate(state, PartyReferenceDataDirectoryEntry.Evolve);
                    return decided.Reference switch
                    {
                        { } reference => new UpdatePartyReferenceAcknowledged(
                            command.EntryId, command.PartyReferenceId, history.Count + events.Count, reference),
                        null => throw new UnreachableException("An accepted Reference Update leaves a Reference record."),
                    };
                }),
        };
    }

    // The party type of each registered entry the changes set a reference to. The entry itself is not loaded again: the
    // decision refuses a reference to it. An entry that is not registered (an unknown id, or the id of another stream type,
    // which loads no entry events) is left out, so the decision answers it as a value.
    private static async Task<IReadOnlyDictionary<PartyReferenceDataDirectoryEntryId, PartyType>> LoadReferencedPartiesAsync(
        IPartyDirectory directory,
        UpdatePartyReference command,
        CancellationToken cancellationToken)
    {
        var parties = new Dictionary<PartyReferenceDataDirectoryEntryId, PartyType>();
        foreach (var entryId in command.Changes.ReferencedEntries().Select(reference => reference.EntryId).Distinct())
        {
            if (entryId == command.EntryId)
            {
                continue;
            }

            var referenced = (await directory.LoadEntryAsync(entryId, cancellationToken))
                .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
            if (referenced is { IsRegistered: true, PartyType: { } partyType })
            {
                parties[entryId] = partyType;
            }
        }

        return parties;
    }
}
