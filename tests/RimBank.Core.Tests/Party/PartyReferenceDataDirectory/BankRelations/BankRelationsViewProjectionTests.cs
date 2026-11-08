using System.Text.Json;
using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.BankRelationsExamples;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntryViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryViewProjection,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryView,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The BankRelations section of the entry's view (roadmap 1.12): Given events → Then view, so it can always be rebuilt
// from the entry's stream, including one written before 1.12.
public sealed class BankRelationsViewProjectionTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private static PartyReferenceDataDirectoryEntryView RegisteredView(long version) =>
        new(EntryId.Value, version, PartyType.Person, OpenDate, Demographics, Reference, BankRelations);

    [Fact]
    public void Updated_bank_relations_show_in_the_view_at_the_next_version() =>
        EntryViewSpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .Then(RegisteredView(2) with { BankRelations = ManagedRelation });

    [Fact]
    public void The_last_update_wins_and_other_sections_are_kept() =>
        EntryViewSpecification.Given(
                Registered(),
                RelationsUpdated(ManagedRelation),
                ReferenceUpdated(),
                RelationsUpdated(BankRelationsDetails.Empty))
            .Then(RegisteredView(4) with { Reference = ReferenceDetails, BankRelations = BankRelationsDetails.Empty });

    [Fact]
    public void A_stream_written_before_1_12_has_no_bank_relations_section() =>
        EntryViewSpecification.Given(Registered(), DetailsUpdated(), ReferenceUpdated())
            .Then(RegisteredView(3) with { Demographics = new DemographicsRecord(Details, DemographicsVerificationStatus.Unverified, null), Reference = ReferenceDetails });

    [Fact]
    public void The_view_s_bank_relations_survive_a_json_round_trip_and_a_view_stored_before_1_12_still_loads()
    {
        var view = RegisteredView(2) with { BankRelations = ManagedRelation };
        var json = JsonSerializer.Serialize(view, _json);

        JsonSerializer.Deserialize<PartyReferenceDataDirectoryEntryView>(json, _json).ShouldBe(view);
        json.ShouldNotContain("CurrentBankRelations");
        JsonSerializer.Deserialize<PartyReferenceDataDirectoryEntryView>(JsonSerializer.Serialize(RegisteredView(1), _json), _json)
            .ShouldNotBeNull().CurrentBankRelations.ShouldBe(BankRelationsDetails.Empty);
    }
}
