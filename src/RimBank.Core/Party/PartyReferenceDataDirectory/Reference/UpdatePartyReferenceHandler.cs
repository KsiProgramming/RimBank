using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdatePartyReference"/> on the write side (tech-stack §8.1, ADR 0024, ADR 0027).</summary>
public static class UpdatePartyReferenceHandler
{
    /// <summary>
    /// Loads the entry; checks that it exists, that the Reference id is its own and that the caller's expected version
    /// is current; decides the Update and appends it with that expected version.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.party-not-found</c>,
    /// <c>party.behavior-qualifier-not-found</c> or <c>party.version-conflict</c>.
    /// </returns>
    public static async Task<Result<UpdatePartyReferenceAcknowledged>> HandleAsync(
        UpdatePartyReference command,
        IPartyDirectory directory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);

        var history = await directory.LoadEntryAsync(command.EntryId, cancellationToken);
        var state = history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        return (state.IsRegistered, state.PartyReferenceId == command.PartyReferenceId, history.Count == command.ExpectedVersion) switch
        {
            (false, _, _) => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (true, false, _) => PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (true, true, false) => PartyReferenceDataDirectoryEntryErrors.VersionConflict,
            (true, true, true) => PartyReferenceDataDirectoryEntry
                .Decide(new UpdatePartyReferenceDetails(command.PartyReferenceId, command.Changes), state)
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
}
