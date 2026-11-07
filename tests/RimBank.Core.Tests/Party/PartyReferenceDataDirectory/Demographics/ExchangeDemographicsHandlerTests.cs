using System.Text.Json;
using RimBank.SharedKernel;
using RimBank.SharedKernel.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Demographics Exchange flow on the command side (roadmap 1.9, ADR 0027, ADR 0028): the handler checks the entry,
// the Demographics id and the expected version, decides with the current time as the verification date, appends and
// acknowledges from the decision.
public sealed class ExchangeDemographicsHandlerTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private static readonly DemographicsId _otherDemographics = DemographicsId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5)));

    private readonly FixedTimeProvider _time = new(VerificationDate);
    private readonly InMemoryPartyDirectory _directory = new();

    [Fact]
    public async Task Verifying_demographics_appends_the_exchange_and_acknowledges_the_next_version()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated());

        var acknowledged = (await Exchange(Demographics, expectedVersion: 2, ExchangeActionType.Verify)).Value;

        _directory.StreamOf(EntryId).ShouldBe([Registered(), DetailsUpdated(), DetailsExchanged(DemographicsVerificationStatus.Verified)]);
        acknowledged.ShouldBe(new ExchangeDemographicsAcknowledged(
            EntryId, Demographics, 3, new DemographicsRecord(Details, DemographicsVerificationStatus.Verified, _time.GetUtcNow())));
    }

    [Fact]
    public async Task Rejecting_demographics_keeps_the_details_and_records_the_rejection()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated());

        var acknowledged = (await Exchange(Demographics, expectedVersion: 2, ExchangeActionType.Reject)).Value;

        acknowledged.Demographics.ShouldBe(new DemographicsRecord(Details, DemographicsVerificationStatus.Rejected, VerificationDate));
    }

    [Fact]
    public async Task Details_never_recorded_are_refused_and_nothing_is_appended()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Exchange(Demographics, expectedVersion: 1, ExchangeActionType.Verify);

        result.Error.ShouldBe(DemographicsErrors.DemographicsNotRecorded);
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_nothing_is_appended()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated());

        var result = await Exchange(Demographics, expectedVersion: 1, ExchangeActionType.Verify);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        _directory.StreamOf(EntryId).ShouldBe([Registered(), DetailsUpdated()]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found()
    {
        var result = await Exchange(Demographics, expectedVersion: 1, ExchangeActionType.Verify);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
    }

    [Fact]
    public async Task A_foreign_demographics_id_is_not_found_before_the_version_is_checked()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated());

        var result = await Exchange(_otherDemographics, expectedVersion: 9, ExchangeActionType.Verify);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
    }

    [Fact]
    public async Task The_acknowledgement_survives_a_json_round_trip()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated());

        var acknowledged = (await Exchange(Demographics, expectedVersion: 2, ExchangeActionType.Verify)).Value;

        var json = JsonSerializer.Serialize(acknowledged, _json);
        JsonSerializer.Deserialize<ExchangeDemographicsAcknowledged>(json, _json).ShouldBe(acknowledged);
    }

    private Task<Result<ExchangeDemographicsAcknowledged>> Exchange(
        DemographicsId demographicsId,
        long expectedVersion,
        ExchangeActionType action) =>
        ExchangeDemographicsHandler.HandleAsync(
            new ExchangeDemographics(EntryId, demographicsId, expectedVersion, action, IdempotencyKey.From("exchange-1")),
            _directory,
            _time,
            TestContext.Current.CancellationToken);
}
