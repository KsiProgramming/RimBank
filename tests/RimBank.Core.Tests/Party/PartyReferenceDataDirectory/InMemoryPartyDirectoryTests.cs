using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using static RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The in-memory write port behaves like the event store it stands in for, so the handler tests mean something.
public sealed class InMemoryPartyDirectoryTests
{
    [Fact]
    public async Task An_unknown_entry_has_no_history()
    {
        var directory = new InMemoryPartyDirectory();

        var history = await directory.LoadEntryAsync(EntryId, TestContext.Current.CancellationToken);

        history.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_started_entry_loads_its_events_in_order()
    {
        var directory = new InMemoryPartyDirectory();
        PartyReferenceDataDirectoryEntryEvent[] events = [Registered(PartyType.Person), Registered(PartyType.Organisation)];

        directory.StartEntry(EntryId, events);

        var history = await directory.LoadEntryAsync(EntryId, TestContext.Current.CancellationToken);
        history.ShouldBe(events);
        directory.StreamOf(EntryId).ShouldBe(events);
    }

    [Fact]
    public void An_entry_stream_starts_only_once()
    {
        var directory = new InMemoryPartyDirectory();
        directory.StartEntry(EntryId, [Registered()]);

        Should.Throw<InvalidOperationException>(() => directory.StartEntry(EntryId, [Registered()]));
    }

    [Fact]
    public async Task A_seeded_entry_loads_its_history()
    {
        var directory = new InMemoryPartyDirectory();

        directory.Seed(EntryId, Registered());

        var history = await directory.LoadEntryAsync(EntryId, TestContext.Current.CancellationToken);
        history.ShouldBe([Registered()]);
    }
}
