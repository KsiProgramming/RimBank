using System.Text.Json;
using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Demographics Update flow on the command side (roadmap 1.9, ADR 0027): the handler checks the entry, the
// Demographics id and the expected version, in that order, decides, appends with that version and acknowledges from
// the decision.
public sealed class UpdateDemographicsHandlerTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private static readonly DemographicsId _otherDemographics = DemographicsId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5)));

    private readonly InMemoryPartyDirectory _directory = new();

    [Fact]
    public async Task Updating_demographics_appends_the_update_and_acknowledges_the_next_version()
    {
        _directory.Seed(EntryId, Registered());

        var acknowledged = (await Update(Demographics, expectedVersion: 1)).Value;

        _directory.StreamOf(EntryId).ShouldBe([Registered(), DetailsUpdated()]);
        acknowledged.ShouldBe(new UpdateDemographicsAcknowledged(
            EntryId, Demographics, 2, new DemographicsRecord(Details, DemographicsVerificationStatus.Unverified, null)));
    }

    [Fact]
    public async Task The_acknowledgement_is_the_state_the_decision_produced()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated(), DetailsExchanged());

        var acknowledged = (await Update(Demographics, expectedVersion: 3, DemographicsChanges.None)).Value;

        var state = _directory.StreamOf(EntryId).Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        acknowledged.Version.ShouldBe(4);
        acknowledged.Demographics.ShouldBe(state.CurrentDemographics);
        acknowledged.Demographics.VerificationStatus.ShouldBe(DemographicsVerificationStatus.Verified);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task An_expected_version_that_is_not_the_current_one_is_a_conflict_and_nothing_is_appended(long expectedVersion)
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(Demographics, expectedVersion);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found_before_the_demographics_id_and_the_version_are_checked()
    {
        var result = await Update(_otherDemographics, expectedVersion: 7);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
        _directory.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_foreign_demographics_id_is_not_found_before_the_version_is_checked()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(_otherDemographics, expectedVersion: 7);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
        result.Error.Kind.ShouldBe(ErrorKind.NotFound);
        result.Error.Code.ShouldBe("party.behavior-qualifier-not-found");
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task The_acknowledgement_survives_a_json_round_trip()
    {
        _directory.Seed(EntryId, Registered());

        var acknowledged = (await Update(Demographics, expectedVersion: 1)).Value;

        var json = JsonSerializer.Serialize(acknowledged, _json);
        JsonSerializer.Deserialize<UpdateDemographicsAcknowledged>(json, _json).ShouldBe(acknowledged);
    }

    private Task<Result<UpdateDemographicsAcknowledged>> Update(
        DemographicsId demographicsId,
        long expectedVersion,
        DemographicsChanges? changes = null) =>
        UpdateDemographicsHandler.HandleAsync(
            new UpdateDemographics(EntryId, demographicsId, expectedVersion, changes ?? ChangesTo(Details), IdempotencyKey.From("demographics-1")),
            _directory,
            TestContext.Current.CancellationToken);
}
