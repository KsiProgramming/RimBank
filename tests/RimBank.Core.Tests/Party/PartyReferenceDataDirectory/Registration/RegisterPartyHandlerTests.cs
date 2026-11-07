using System.Text.Json;
using RimBank.SharedKernel;
using RimBank.SharedKernel.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Register flow on the command side (BIAN PartyReferenceDataDirectoryEntry/Register, ADR 0024): the handler creates
// the ids and the open date, decides against the stored history, starts the stream and acknowledges from the decision.
public sealed class RegisterPartyHandlerTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private readonly FixedTimeProvider _time = new(OpenDate);
    private readonly InMemoryPartyDirectory _directory = new();

    [Theory]
    [InlineData(PartyType.Person)]
    [InlineData(PartyType.Organisation)]
    public async Task Registering_a_party_starts_its_entry_with_PartyRegistered(PartyType partyType)
    {
        var acknowledged = (await Register(partyType)).Value;

        _directory.Entries.ShouldBe([acknowledged.EntryId]);
        _directory.StreamOf(acknowledged.EntryId).ShouldBe([
            new PartyRegistered(
                acknowledged.EntryId,
                partyType,
                OpenDate,
                acknowledged.DemographicsId,
                acknowledged.PartyReferenceId,
                acknowledged.BankRelationsId),
        ]);
        acknowledged.PartyType.ShouldBe(partyType);
        acknowledged.OpenDate.ShouldBe(OpenDate);
        acknowledged.Version.ShouldBe(1);
    }

    [Fact]
    public async Task The_entry_and_its_behavior_qualifiers_get_new_version_7_ids_at_the_current_time()
    {
        var acknowledged = (await Register()).Value;

        Guid[] ids =
        [
            acknowledged.EntryId.Value,
            acknowledged.DemographicsId.Value,
            acknowledged.PartyReferenceId.Value,
            acknowledged.BankRelationsId.Value,
        ];
        ids.Distinct().Count().ShouldBe(4);
        ids.ShouldAllBe(id => id.Version == 7);
        TypedId.CreatedAt(acknowledged.EntryId).ShouldBe(OpenDate);
        TypedId.CreatedAt(acknowledged.DemographicsId).ShouldBe(OpenDate);
        TypedId.CreatedAt(acknowledged.PartyReferenceId).ShouldBe(OpenDate);
        TypedId.CreatedAt(acknowledged.BankRelationsId).ShouldBe(OpenDate);
    }

    [Fact]
    public async Task The_acknowledgement_is_the_state_the_decision_produced()
    {
        var acknowledged = (await Register(PartyType.Organisation)).Value;

        var state = _directory.StreamOf(acknowledged.EntryId).Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        state.ShouldBe(new PartyReferenceDataDirectoryEntry(
            acknowledged.EntryId,
            acknowledged.PartyType,
            acknowledged.OpenDate,
            acknowledged.DemographicsId,
            acknowledged.PartyReferenceId,
            acknowledged.BankRelationsId));
    }

    [Fact]
    public async Task A_party_type_that_is_neither_person_nor_organisation_is_refused_and_nothing_is_started()
    {
        var result = await Register((PartyType)99);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("party.party-type-invalid");
        _directory.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_entry_that_is_already_registered_is_refused_and_nothing_is_appended()
    {
        var directory = new EveryEntryRegisteredDirectory();

        var result = await RegisterPartyHandler.HandleAsync(
            new RegisterParty(PartyType.Person, IdempotencyKey.From("register-1")),
            directory,
            _time,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        result.Error.Code.ShouldBe("party.party-already-registered");
        directory.Started.ShouldBeFalse();
    }

    [Fact]
    public async Task The_idempotency_key_does_not_change_the_outcome()
    {
        var first = (await Register(PartyType.Person, "register-1")).Value;
        var second = (await Register(PartyType.Person, "register-2")).Value;

        second.ShouldBe(first with
        {
            EntryId = second.EntryId,
            DemographicsId = second.DemographicsId,
            PartyReferenceId = second.PartyReferenceId,
            BankRelationsId = second.BankRelationsId,
        });
    }

    [Fact]
    public async Task The_acknowledgement_survives_a_json_round_trip()
    {
        var acknowledged = (await Register()).Value;

        var json = JsonSerializer.Serialize(acknowledged, _json);

        JsonSerializer.Deserialize<RegisterPartyAcknowledged>(json, _json).ShouldBe(acknowledged);
    }

    private Task<Result<RegisterPartyAcknowledged>> Register(PartyType partyType = PartyType.Person, string key = "register-1") =>
        RegisterPartyHandler.HandleAsync(
            new RegisterParty(partyType, IdempotencyKey.From(key)),
            _directory,
            _time,
            TestContext.Current.CancellationToken);

    // A directory in which every entry is already registered: the handler creates its own new id, so this is the only
    // way to reach the decision's "already registered" path.
    private sealed class EveryEntryRegisteredDirectory : IPartyDirectory
    {
        public bool Started { get; private set; }

        public Task<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> LoadEntryAsync(
            PartyReferenceDataDirectoryEntryId entryId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>>([Registered() with { EntryId = entryId }]);

        public void StartEntry(PartyReferenceDataDirectoryEntryId entryId, IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events) =>
            Started = true;

        public void AppendToEntry(
            PartyReferenceDataDirectoryEntryId entryId,
            long expectedVersion,
            IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events) =>
            Started = true;
    }
}
