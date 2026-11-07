using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Retrieval;
using static RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The in-memory read port serves the views the real projection builds, so the query handler tests mean something.
public sealed class InMemoryPartyDirectoryEnquiryTests
{
    [Fact]
    public async Task An_unknown_entry_has_no_view()
    {
        var enquiry = new InMemoryPartyDirectoryEnquiry();

        var view = await enquiry.FindEntryAsync(EntryId, TestContext.Current.CancellationToken);

        view.ShouldBeNull();
    }

    [Fact]
    public async Task A_seeded_entry_has_the_view_its_events_project()
    {
        var enquiry = new InMemoryPartyDirectoryEnquiry();

        enquiry.Seed(EntryId, Registered(PartyType.Organisation));

        var view = await enquiry.FindEntryAsync(EntryId, TestContext.Current.CancellationToken);
        view.ShouldBe(PartyReferenceDataDirectoryEntryViewProjection.Project(null, Registered(PartyType.Organisation)));
    }
}
