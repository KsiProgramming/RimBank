using System.Text.Json;
using RimBank.SharedKernel;
using RimBank.SharedKernel.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Reference Update flow on the command side (roadmap 1.10, ADR 0027): the handler checks the entry, the Reference id
// and the expected version, in that order, decides, appends with that version and acknowledges from the decision.
public sealed class UpdatePartyReferenceHandlerTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private static readonly PartyReferenceId _otherReference = PartyReferenceId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5)));

    private readonly InMemoryPartyDirectory _directory = new();

    private readonly FixedTimeProvider _time = new(RefreshDate);

    [Fact]
    public async Task Updating_the_reference_appends_the_update_and_acknowledges_the_next_version()
    {
        _directory.Seed(EntryId, Registered());

        var acknowledged = (await Update(Reference, expectedVersion: 1)).Value;

        _directory.StreamOf(EntryId).ShouldBe([Registered(), ReferenceUpdated()]);
        acknowledged.ShouldBe(new UpdatePartyReferenceAcknowledged(EntryId, Reference, 2, ReferenceDetails));
    }

    [Fact]
    public async Task The_acknowledgement_is_the_state_the_decision_produced()
    {
        _directory.Seed(EntryId, Registered(), DetailsUpdated(), ReferenceUpdated());

        var acknowledged = (await Update(Reference, expectedVersion: 3, PartyReferenceChanges.None)).Value;

        var state = _directory.StreamOf(EntryId).Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        acknowledged.Version.ShouldBe(4);
        acknowledged.Reference.ShouldBe(state.CurrentReference);
        acknowledged.Reference.ShouldBe(ReferenceDetails);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task An_expected_version_that_is_not_the_current_one_is_a_conflict_and_nothing_is_appended(long expectedVersion)
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(Reference, expectedVersion);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found_before_the_reference_id_and_the_version_are_checked()
    {
        var result = await Update(_otherReference, expectedVersion: 7);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
        _directory.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_foreign_reference_id_is_not_found_before_the_version_is_checked()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(_otherReference, expectedVersion: 7);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task Today_comes_from_the_clock_so_a_birth_date_after_it_is_refused_and_nothing_is_appended()
    {
        _directory.Seed(EntryId, Registered());
        var bornTomorrow = PartyReferenceChanges.None with { DateOfBirth = new PartyReferenceChange<DateOnly?>(Today.AddDays(1)) };
        var bornToday = PartyReferenceChanges.None with { DateOfBirth = new PartyReferenceChange<DateOnly?>(Today) };

        (await Update(Reference, expectedVersion: 1, bornTomorrow)).Error.ShouldBe(PartyReferenceErrors.DateOfBirthAfterToday);
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);

        (await Update(Reference, expectedVersion: 1, bornToday)).Value.Reference.DateOfBirth.ShouldBe(Today);
    }

    [Fact]
    public async Task The_command_and_the_acknowledgement_survive_a_json_round_trip_with_every_value()
    {
        _directory.Seed(EntryId, Registered());
        var command = Command(Reference, expectedVersion: 1, ReferenceChangesTo(FullReferenceDetails));

        var acknowledged = (await UpdatePartyReferenceHandler.HandleAsync(command, _directory, _time, TestContext.Current.CancellationToken)).Value;

        acknowledged.Reference.ShouldBe(FullReferenceDetails);
        JsonSerializer.Deserialize<UpdatePartyReference>(JsonSerializer.Serialize(command, _json), _json).ShouldBe(command);
        JsonSerializer.Deserialize<UpdatePartyReferenceAcknowledged>(JsonSerializer.Serialize(acknowledged, _json), _json)
            .ShouldBe(acknowledged);
    }

    [Fact]
    public async Task The_command_and_the_acknowledgement_survive_a_json_round_trip()
    {
        _directory.Seed(EntryId, Registered());
        var command = Command(Reference, expectedVersion: 1, ReferenceChangesTo(Identifiers with { TaxIdentificationNumber = null }));

        var acknowledged = (await UpdatePartyReferenceHandler.HandleAsync(command, _directory, _time, TestContext.Current.CancellationToken)).Value;

        JsonSerializer.Deserialize<UpdatePartyReference>(JsonSerializer.Serialize(command, _json), _json).ShouldBe(command);
        JsonSerializer.Deserialize<UpdatePartyReferenceAcknowledged>(JsonSerializer.Serialize(acknowledged, _json), _json)
            .ShouldBe(acknowledged);
    }

    private static UpdatePartyReference Command(PartyReferenceId referenceId, long expectedVersion, PartyReferenceChanges? changes = null) =>
        new(EntryId, referenceId, expectedVersion, changes ?? ReferenceChangesTo(Identifiers), IdempotencyKey.From("reference-1"));

    private Task<Result<UpdatePartyReferenceAcknowledged>> Update(
        PartyReferenceId referenceId,
        long expectedVersion,
        PartyReferenceChanges? changes = null) =>
        UpdatePartyReferenceHandler.HandleAsync(
            Command(referenceId, expectedVersion, changes),
            _directory,
            _time,
            TestContext.Current.CancellationToken);
}
