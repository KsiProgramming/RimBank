using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdateBankRelations"/> on the write side (tech-stack §8.1, ADR 0024, ADR 0027, ADR 0035).</summary>
public static class UpdateBankRelationsHandler
{
    /// <summary>
    /// Loads the entry; checks that it exists, that the BankRelations id is its own and that the caller's expected version
    /// is current; loads the employee or unit to check through the write port, never a view; decides the Update and
    /// appends it with that expected version.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="cancellationToken">Cancels the loads.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.party-not-found</c>,
    /// <c>party.behavior-qualifier-not-found</c>, <c>party.version-conflict</c> or the decision's error (including
    /// <c>party.self-reference</c>, <c>party.referenced-party-not-registered</c> and
    /// <c>party.referenced-party-type-mismatch</c>).
    /// </returns>
    public static async Task<Result<UpdateBankRelationsAcknowledged>> HandleAsync(
        UpdateBankRelations command,
        IPartyDirectory directory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);

        var history = await directory.LoadEntryAsync(command.EntryId, cancellationToken);
        var state = history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        var refused = (state.IsRegistered, state.BankRelationsId == command.BankRelationsId, history.Count == command.ExpectedVersion) switch
        {
            (false, _, _) => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (true, false, _) => PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (true, true, false) => PartyReferenceDataDirectoryEntryErrors.VersionConflict,
            _ => null,
        };
        if (refused is not null)
        {
            return refused;
        }

        var referencedPartyType = await LoadReferencedPartyTypeAsync(
            directory, command.EntryId, state.CurrentBankRelations.ReferenceToCheck(command.Changes), cancellationToken);

        return PartyReferenceDataDirectoryEntry
            .Decide(new UpdateBankRelationsDetails(command.BankRelationsId, command.Changes, referencedPartyType), state)
            .Map(events =>
            {
                directory.AppendToEntry(command.EntryId, command.ExpectedVersion, events);
                var decided = events.Aggregate(state, PartyReferenceDataDirectoryEntry.Evolve);
                return decided.BankRelations switch
                {
                    { } bankRelations => new UpdateBankRelationsAcknowledged(
                        command.EntryId, command.BankRelationsId, history.Count + events.Count, bankRelations),
                    null => throw new UnreachableException("An accepted BankRelations Update leaves a BankRelations record."),
                };
            });
    }

    // The party type of the employee or unit, if it is a registered entry. The entry itself is not loaded again (the
    // decision refuses it), and an id of another stream type loads no entry events, so it is "not registered".
    private static async Task<PartyType?> LoadReferencedPartyTypeAsync(
        IPartyDirectory directory,
        PartyReferenceDataDirectoryEntryId entryId,
        PartyReferenceDataDirectoryEntryId? referenced,
        CancellationToken cancellationToken)
    {
        if (referenced is not { } other || other == entryId)
        {
            return null;
        }

        var state = (await directory.LoadEntryAsync(other, cancellationToken))
            .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        return state.IsRegistered ? state.PartyType : null;
    }
}
