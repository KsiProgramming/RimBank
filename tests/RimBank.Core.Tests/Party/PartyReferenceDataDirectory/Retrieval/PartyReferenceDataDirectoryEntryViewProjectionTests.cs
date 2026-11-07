using System.Text.Json;
using System.Text.Json.Nodes;
using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntryViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryViewProjection,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryView,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The entry's view (ADR 0024): the fold of the entry's events from no view, so it can always be rebuilt.
public sealed class PartyReferenceDataDirectoryEntryViewProjectionTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    [Theory]
    [InlineData(PartyType.Person)]
    [InlineData(PartyType.Organisation)]
    public void A_registered_party_has_a_view_at_version_1(PartyType partyType) =>
        EntryViewSpecification.Given(Registered(partyType))
            .Then(new PartyReferenceDataDirectoryEntryView(
                EntryId.Value,
                Version: 1,
                partyType,
                OpenDate,
                Demographics,
                Reference,
                BankRelations));

    [Fact]
    public void An_updated_entry_shows_the_new_party_type_and_refresh_date_at_the_next_version() =>
        EntryViewSpecification.Given(Registered(PartyType.Person), Updated(PartyType.Organisation))
            .Then(new PartyReferenceDataDirectoryEntryView(
                EntryId.Value,
                Version: 2,
                PartyType.Organisation,
                OpenDate,
                Demographics,
                Reference,
                BankRelations,
                RefreshDate));

    [Fact]
    public void Updated_and_exchanged_demographics_show_in_the_view_at_the_stream_version() =>
        EntryViewSpecification.Given(Registered(), DetailsUpdated(), DetailsExchanged(DemographicsVerificationStatus.Verified))
            .Then(new PartyReferenceDataDirectoryEntryView(
                EntryId.Value,
                Version: 3,
                PartyType.Person,
                OpenDate,
                Demographics,
                Reference,
                BankRelations,
                Demographics: new DemographicsRecord(Details, DemographicsVerificationStatus.Verified, VerificationDate)));

    [Fact]
    public void Changed_demographics_show_unverified_again() =>
        EntryViewSpecification.Given(
                Registered(),
                DetailsUpdated(),
                DetailsExchanged(),
                DetailsUpdated(Details with { EducationHistory = "PhD" }))
            .Then(new PartyReferenceDataDirectoryEntryView(
                EntryId.Value,
                Version: 4,
                PartyType.Person,
                OpenDate,
                Demographics,
                Reference,
                BankRelations,
                Demographics: new DemographicsRecord(Details with { EducationHistory = "PhD" }, DemographicsVerificationStatus.Unverified, null)));

    [Fact]
    public void A_registered_entry_has_no_demographics_section_and_reads_as_empty()
    {
        var view = PartyReferenceDataDirectoryEntryViewProjection.Project(null, Registered()).ShouldNotBeNull();

        view.Demographics.ShouldBeNull();
        view.CurrentDemographics.ShouldBe(DemographicsRecord.Empty);
    }

    [Fact]
    public void A_demographics_event_before_registration_leaves_no_view() =>
        EntryViewSpecification.Given(DetailsUpdated(), DetailsExchanged()).ThenNoView();

    [Fact]
    public void A_registered_entry_has_no_refresh_date_in_its_view() =>
        PartyReferenceDataDirectoryEntryViewProjection.Project(null, Registered()).ShouldNotBeNull().RefreshDate.ShouldBeNull();

    [Fact]
    public void An_update_before_registration_leaves_no_view() =>
        EntryViewSpecification.Given(Updated()).ThenNoView();

    [Fact]
    public void An_entry_with_no_events_has_no_view() =>
        EntryViewSpecification.Given().ThenNoView();

    [Fact]
    public void Every_later_entry_event_bumps_the_version_so_it_stays_the_stream_version() =>
        EntryViewSpecification.Given(Registered(), new LaterEntryEvent(), new LaterEntryEvent())
            .Then(new PartyReferenceDataDirectoryEntryView(
                EntryId.Value,
                Version: 3,
                PartyType.Person,
                OpenDate,
                Demographics,
                Reference,
                BankRelations));

    [Fact]
    public void An_entry_event_before_registration_leaves_no_view() =>
        EntryViewSpecification.Given(new LaterEntryEvent()).ThenNoView();

    [Fact]
    public void The_view_names_its_entry_with_the_typed_id()
    {
        var view = PartyReferenceDataDirectoryEntryViewProjection.Project(null, Registered());

        view.ShouldNotBeNull().EntryId.ShouldBe(EntryId);
    }

    [Fact]
    public void The_view_survives_a_json_round_trip_without_storing_the_typed_id()
    {
        var view = new PartyReferenceDataDirectoryEntryEvent[] { Registered(), Updated(), DetailsUpdated(), DetailsExchanged() }
            .Aggregate((PartyReferenceDataDirectoryEntryView?)null, PartyReferenceDataDirectoryEntryViewProjection.Project)
            .ShouldNotBeNull();

        var json = JsonSerializer.Serialize(view, _json);

        JsonSerializer.Deserialize<PartyReferenceDataDirectoryEntryView>(json, _json).ShouldBe(view);
        var stored = JsonNode.Parse(json).ShouldNotBeNull().AsObject();
        stored.ContainsKey(nameof(PartyReferenceDataDirectoryEntryView.EntryId)).ShouldBeFalse();
        stored.ContainsKey(nameof(PartyReferenceDataDirectoryEntryView.CurrentDemographics)).ShouldBeFalse();
    }

    // Stands in for the entry's later events whose data the view does not show (the Behavior Qualifier updates) until they exist.
    private sealed record LaterEntryEvent : PartyReferenceDataDirectoryEntryEvent;
}
