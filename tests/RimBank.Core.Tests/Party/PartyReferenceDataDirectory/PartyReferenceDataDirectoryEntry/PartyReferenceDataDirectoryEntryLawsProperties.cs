using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using RimBank.Party.PartyReferenceDataDirectory.BankRelations;
using RimBank.Party.PartyReferenceDataDirectory.Demographics;
using RimBank.Party.PartyReferenceDataDirectory.Reference;
using EntryLaws = RimBank.SharedKernel.Testing.DeciderLaws<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests;

// The decider laws on the entry (ADR 0010). Each runs 100 command sequences.
public sealed class PartyReferenceDataDirectoryEntryLawsProperties
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    // Two of each id and two open dates: sequences repeat Register often, so both acceptance and
    // party.party-already-registered are common, and an undefined party type now and then covers party.party-type-invalid.
    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _commands =
        from entryId in Pick(PartyReferenceDataDirectoryEntryId.From)
        from partyType in Gen.Frequency(
            (4, Gen.Elements(PartyType.Person, PartyType.Organisation)),
            (1, Gen.Elements((PartyType)(-1), (PartyType)2, (PartyType)99)))
        from openDate in Gen.Elements(_openDate, _openDate.AddDays(1))
        from demographicsId in Pick(DemographicsId.From)
        from partyReferenceId in Pick(PartyReferenceId.From)
        from bankRelationsId in Pick(BankRelationsId.From)
        select (PartyReferenceDataDirectoryEntryCommand)new RegisterPartyReferenceDataDirectoryEntry(
            entryId, partyType, openDate, demographicsId, partyReferenceId, bankRelationsId);

    [Property]
    public Property Decisions_are_deterministic() => EntryLaws.DecisionsAreDeterministic(_commands);

    [Property]
    public Property Decided_events_evolve() => EntryLaws.DecidedEventsEvolve(_commands);

    private static Gen<TId> Pick<TId>(Func<Guid, TId> from) =>
        Gen.Elements(from(Guid.CreateVersion7(_openDate)), from(Guid.CreateVersion7(_openDate.AddMinutes(1))));
}
