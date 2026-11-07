using System.Diagnostics;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The entry's state and Evolve (ADR 0010, ADR 0024).
public sealed class PartyReferenceDataDirectoryEntryTests
{
    [Fact]
    public void The_initial_entry_is_not_registered()
    {
        var entry = PartyReferenceDataDirectoryEntry.Initial;

        entry.IsRegistered.ShouldBeFalse();
        entry.Id.ShouldBeNull();
    }

    [Fact]
    public void A_registered_entry_holds_what_PartyRegistered_recorded()
    {
        var entry = PartyReferenceDataDirectoryEntry.Evolve(PartyReferenceDataDirectoryEntry.Initial, Registered(PartyType.Organisation));

        entry.IsRegistered.ShouldBeTrue();
        entry.ShouldBe(new PartyReferenceDataDirectoryEntry(
            EntryId, PartyType.Organisation, OpenDate, Demographics, Reference, BankRelations));
    }

    [Fact]
    public void Evolving_an_event_the_entry_does_not_know_is_a_programmer_error()
    {
        Should.Throw<UnreachableException>(() =>
            PartyReferenceDataDirectoryEntry.Evolve(PartyReferenceDataDirectoryEntry.Initial, new UnknownEvent()));
    }

    private sealed record UnknownEvent : PartyReferenceDataDirectoryEntryEvent;
}
