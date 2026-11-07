using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The write-side steps the Demographics command handlers share (tech-stack §8.1, ADR 0027): load the entry, check in
/// order that it exists, that the Demographics id is its own and that the caller's expected version is current, then
/// decide, append with that version and acknowledge from the evolved state.
/// </summary>
internal static class DemographicsCommandFlow
{
    public static async Task<Result<TAcknowledged>> RunAsync<TAcknowledged>(
        IPartyDirectory directory,
        PartyReferenceDataDirectoryEntryId entryId,
        DemographicsId demographicsId,
        long expectedVersion,
        PartyReferenceDataDirectoryEntryCommand decision,
        Func<PartyReferenceDataDirectoryEntryId, DemographicsId, long, DemographicsRecord, TAcknowledged> acknowledge,
        CancellationToken cancellationToken)
    {
        var history = await directory.LoadEntryAsync(entryId, cancellationToken);
        var state = history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        return (state.IsRegistered, state.DemographicsId == demographicsId, history.Count == expectedVersion) switch
        {
            (false, _, _) => PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (true, false, _) => PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (true, true, false) => PartyReferenceDataDirectoryEntryErrors.VersionConflict,
            (true, true, true) => PartyReferenceDataDirectoryEntry
                .Decide(decision, state)
                .Map(events =>
                {
                    directory.AppendToEntry(entryId, expectedVersion, events);
                    var decided = events.Aggregate(state, PartyReferenceDataDirectoryEntry.Evolve);
                    return decided.Demographics switch
                    {
                        { } demographics => acknowledge(entryId, demographicsId, history.Count + events.Count, demographics),
                        null => throw new UnreachableException("An accepted Demographics command leaves a Demographics record."),
                    };
                }),
        };
    }
}
