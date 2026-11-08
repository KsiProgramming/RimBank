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
    public void An_updated_entry_has_the_new_party_type_and_refresh_date_and_keeps_the_rest()
    {
        var entry = new PartyReferenceDataDirectoryEntryEvent[] { Registered(PartyType.Person), Updated(PartyType.Organisation) }
            .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        entry.ShouldBe(new PartyReferenceDataDirectoryEntry(
            EntryId, PartyType.Organisation, OpenDate, Demographics, Reference, BankRelations, RefreshDate));
    }

    [Fact]
    public void A_registered_entry_has_no_refresh_date_until_it_is_updated() =>
        PartyReferenceDataDirectoryEntry.Evolve(PartyReferenceDataDirectoryEntry.Initial, Registered()).RefreshDate.ShouldBeNull();

    [Fact]
    public void A_registered_entry_has_empty_unverified_demographics_until_they_are_updated()
    {
        var entry = PartyReferenceDataDirectoryEntry.Evolve(PartyReferenceDataDirectoryEntry.Initial, Registered());

        entry.Demographics.ShouldBeNull();
        entry.CurrentDemographics.ShouldBe(DemographicsRecord.Empty);
    }

    [Fact]
    public void Updated_demographics_hold_what_the_event_recorded_and_keep_the_rest_of_the_entry()
    {
        var entry = new PartyReferenceDataDirectoryEntryEvent[] { Registered(), Updated(), DetailsUpdated() }
            .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        entry.ShouldBe(new PartyReferenceDataDirectoryEntry(
            EntryId, PartyType.Organisation, OpenDate, Demographics, Reference, BankRelations, RefreshDate,
            new DemographicsRecord(Details, DemographicsVerificationStatus.Unverified, null)));
    }

    [Theory]
    [InlineData(DemographicsVerificationStatus.Verified)]
    [InlineData(DemographicsVerificationStatus.Rejected)]
    public void Exchanged_demographics_keep_their_details_and_take_the_verification(DemographicsVerificationStatus status)
    {
        var entry = new PartyReferenceDataDirectoryEntryEvent[] { Registered(), DetailsUpdated(), DetailsExchanged(status) }
            .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        entry.CurrentDemographics.ShouldBe(new DemographicsRecord(Details, status, VerificationDate));
    }

    [Fact]
    public void A_registered_entry_has_an_empty_reference_until_it_is_updated()
    {
        var entry = PartyReferenceDataDirectoryEntry.Evolve(PartyReferenceDataDirectoryEntry.Initial, Registered());

        entry.Reference.ShouldBeNull();
        entry.CurrentReference.ShouldBe(PartyReferenceDetails.Empty);
    }

    [Fact]
    public void An_updated_reference_holds_what_the_event_recorded_and_keeps_the_rest_of_the_entry()
    {
        var entry = new PartyReferenceDataDirectoryEntryEvent[] { Registered(), Updated(), DetailsUpdated(), ReferenceUpdated() }
            .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        entry.ShouldBe(new PartyReferenceDataDirectoryEntry(
            EntryId, PartyType.Organisation, OpenDate, Demographics, Reference, BankRelations, RefreshDate,
            new DemographicsRecord(Details, DemographicsVerificationStatus.Unverified, null),
            ReferenceDetails));
    }

    [Fact]
    public void Evolving_an_event_the_entry_does_not_know_is_a_programmer_error()
    {
        Should.Throw<UnreachableException>(() =>
            PartyReferenceDataDirectoryEntry.Evolve(PartyReferenceDataDirectoryEntry.Initial, new UnknownEvent()));
    }

    private sealed record UnknownEvent : PartyReferenceDataDirectoryEntryEvent;
}
