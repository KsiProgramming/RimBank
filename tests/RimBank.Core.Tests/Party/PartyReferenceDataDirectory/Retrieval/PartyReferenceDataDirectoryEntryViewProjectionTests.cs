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
        var view = PartyReferenceDataDirectoryEntryViewProjection.Project(null, Registered(PartyType.Organisation)).ShouldNotBeNull();

        var json = JsonSerializer.Serialize(view, _json);

        JsonSerializer.Deserialize<PartyReferenceDataDirectoryEntryView>(json, _json).ShouldBe(view);
        JsonNode.Parse(json).ShouldNotBeNull().AsObject().ContainsKey(nameof(PartyReferenceDataDirectoryEntryView.EntryId)).ShouldBeFalse();
    }

    // Stands in for the entry's later events (Update, 1.8, and the Behavior Qualifier updates) until they exist.
    private sealed record LaterEntryEvent : PartyReferenceDataDirectoryEntryEvent;
}
