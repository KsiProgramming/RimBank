using System.Text.Json;
using RimBank.SharedKernel;
using RimBank.SharedKernel.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Update flow on the command side (BIAN PartyReferenceDataDirectoryEntry/Update, roadmap 1.8, ADR 0027): the handler
// checks the entry and the caller's expected version, decides with the current time as the refresh date, appends with
// that version and acknowledges from the decision.
public sealed class UpdatePartyReferenceDataDirectoryEntryHandlerTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private readonly FixedTimeProvider _time = new(RefreshDate);
    private readonly InMemoryPartyDirectory _directory = new();

    [Fact]
    public async Task Updating_an_entry_appends_the_update_and_acknowledges_the_next_version()
    {
        _directory.Seed(EntryId, Registered(PartyType.Person));

        var acknowledged = (await Update(expectedVersion: 1, PartyType.Organisation)).Value;

        _directory.StreamOf(EntryId).ShouldBe([Registered(PartyType.Person), Updated(PartyType.Organisation)]);
        acknowledged.ShouldBe(new UpdatePartyReferenceDataDirectoryEntryAcknowledged(
            EntryId, 2, PartyType.Organisation, OpenDate, RefreshDate, Demographics, Reference, BankRelations));
    }

    [Fact]
    public async Task An_update_without_a_party_type_keeps_it_and_takes_the_refresh_date_from_the_clock()
    {
        _directory.Seed(EntryId, Registered(PartyType.Organisation));

        var acknowledged = (await Update(expectedVersion: 1, partyType: null)).Value;

        acknowledged.PartyType.ShouldBe(PartyType.Organisation);
        acknowledged.RefreshDate.ShouldBe(_time.GetUtcNow());
    }

    [Fact]
    public async Task The_acknowledgement_is_the_state_the_decision_produced()
    {
        _directory.Seed(EntryId, Registered(PartyType.Person), Updated(PartyType.Organisation));

        var acknowledged = (await Update(expectedVersion: 2, PartyType.Person)).Value;

        var state = _directory.StreamOf(EntryId).Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        acknowledged.Version.ShouldBe(3);
        state.ShouldBe(new PartyReferenceDataDirectoryEntry(
            acknowledged.EntryId,
            acknowledged.PartyType,
            acknowledged.OpenDate,
            acknowledged.DemographicsId,
            acknowledged.PartyReferenceId,
            acknowledged.BankRelationsId,
            acknowledged.RefreshDate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public async Task An_expected_version_that_is_not_the_current_one_is_a_conflict_and_nothing_is_appended(long expectedVersion)
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(expectedVersion, PartyType.Organisation);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        result.Error.Code.ShouldBe("party.version-conflict");
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found_whatever_the_expected_version()
    {
        var result = await Update(expectedVersion: 1, PartyType.Organisation);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
        _directory.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_refused_decision_appends_nothing()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(expectedVersion: 1, (PartyType)99);

        result.Error.Code.ShouldBe("party.party-type-invalid");
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task The_acknowledgement_survives_a_json_round_trip()
    {
        _directory.Seed(EntryId, Registered());

        var acknowledged = (await Update(expectedVersion: 1, PartyType.Organisation)).Value;

        var json = JsonSerializer.Serialize(acknowledged, _json);
        JsonSerializer.Deserialize<UpdatePartyReferenceDataDirectoryEntryAcknowledged>(json, _json).ShouldBe(acknowledged);
    }

    private Task<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>> Update(long expectedVersion, PartyType? partyType) =>
        UpdatePartyReferenceDataDirectoryEntryHandler.HandleAsync(
            new UpdatePartyReferenceDataDirectoryEntry(EntryId, expectedVersion, partyType, IdempotencyKey.From("update-1")),
            _directory,
            _time,
            TestContext.Current.CancellationToken);
}
