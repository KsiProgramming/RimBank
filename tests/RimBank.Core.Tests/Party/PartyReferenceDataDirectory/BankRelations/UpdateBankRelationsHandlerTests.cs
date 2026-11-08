using System.Text.Json;
using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.BankRelationsExamples;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The BankRelations Update flow on the command side (roadmap 1.12, ADR 0035): the handler checks the entry, the
// BankRelations id and the expected version, in that order, loads the employee or unit to check from the write side,
// decides, appends with that version and acknowledges from the decision.
public sealed class UpdateBankRelationsHandlerTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    private static readonly BankRelationsId _otherBankRelations = BankRelationsId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5)));

    private readonly InMemoryPartyDirectory _directory = new();

    [Fact]
    public async Task Updating_the_bank_relations_loads_the_employee_appends_and_acknowledges_the_next_version()
    {
        _directory.Seed(EntryId, Registered());
        _directory.Seed(ManagerId, RegisteredEntry(ManagerId, PartyType.Person));

        var acknowledged = (await Update(BankRelations, expectedVersion: 1, BankRelationsChanges.To(ManagedRelation))).Value;

        _directory.StreamOf(EntryId).ShouldBe([Registered(), RelationsUpdated(ManagedRelation)]);
        _directory.StreamOf(ManagerId).ShouldBe([RegisteredEntry(ManagerId, PartyType.Person)]);
        acknowledged.ShouldBe(new UpdateBankRelationsAcknowledged(EntryId, BankRelations, 2, ManagedRelation));
    }

    [Fact]
    public async Task The_acknowledgement_is_the_state_the_decision_produced()
    {
        _directory.Seed(EntryId, Registered(), ReferenceUpdated(), RelationsUpdated(ManagedRelation));

        var acknowledged = (await Update(BankRelations, expectedVersion: 3, BankRelationsChanges.None)).Value;

        var state = _directory.StreamOf(EntryId).Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);
        acknowledged.Version.ShouldBe(4);
        acknowledged.BankRelations.ShouldBe(state.CurrentBankRelations);
        acknowledged.BankRelations.ShouldBe(ManagedRelation);
    }

    [Fact]
    public async Task A_kept_reference_is_not_checked_again()
    {
        // The manager's entry is not in this directory any more: only a load would find it missing.
        _directory.Seed(EntryId, Registered(), RelationsUpdated(ManagedRelation));

        var result = await Update(BankRelations, expectedVersion: 2, new BankRelationsChanges(LifecycleStatus: new(BankRelationLifecycleStatus.Suspended)));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_new_type_is_checked_against_the_kept_reference_as_loaded()
    {
        _directory.Seed(EntryId, Registered(), RelationsUpdated(ManagedRelation));
        _directory.Seed(ManagerId, RegisteredEntry(ManagerId, PartyType.Person));

        var result = await Update(BankRelations, expectedVersion: 2, new BankRelationsChanges(Type: new(BankRelationType.ServicingBranch)));

        result.Error.Code.ShouldBe("party.referenced-party-type-mismatch");
        _directory.StreamOf(EntryId).Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_party_type_loaded_is_the_current_one()
    {
        _directory.Seed(EntryId, Registered());
        _directory.Seed(BranchId, RegisteredEntry(BranchId, PartyType.Person), new PartyReferenceDataDirectoryEntryUpdated(BranchId, PartyType.Organisation, RefreshDate));

        var result = await Update(BankRelations, expectedVersion: 1, new BankRelationsChanges(new(BankRelationType.ServicingBranch), new(BranchId)));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task An_employee_that_is_not_registered_is_refused_and_nothing_is_appended()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(BankRelations, expectedVersion: 1, BankRelationsChanges.To(ManagedRelation));

        result.Error.Code.ShouldBe("party.referenced-party-not-registered");
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task An_association_id_given_as_the_employee_is_not_registered_never_a_failure()
    {
        _directory.Seed(EntryId, Registered());
        var associationId = PartyAssociationId.From(ManagerId.Value);
        _directory.Seed(associationId, new PartyAssociationRegistered(
            associationId, EntryId, CompanyId, PartyRelationshipType.PartyIsSubsidiaryOfParty, null));

        var result = await Update(BankRelations, expectedVersion: 1, BankRelationsChanges.To(ManagedRelation));

        result.Error.Code.ShouldBe("party.referenced-party-not-registered");
    }

    [Fact]
    public async Task The_entry_itself_as_its_employee_is_refused()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(BankRelations, expectedVersion: 1, new BankRelationsChanges(new(BankRelationType.RelationshipManager), new(EntryId)));

        result.Error.Code.ShouldBe("party.self-reference");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task An_expected_version_that_is_not_the_current_one_is_a_conflict_and_nothing_is_appended(long expectedVersion)
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(BankRelations, expectedVersion, BankRelationsChanges.None);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        _directory.StreamOf(EntryId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found_before_the_id_and_the_version_are_checked()
    {
        var result = await Update(_otherBankRelations, expectedVersion: 7, BankRelationsChanges.None);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
        _directory.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_foreign_bank_relations_id_is_not_found_before_the_version_is_checked()
    {
        _directory.Seed(EntryId, Registered());

        var result = await Update(_otherBankRelations, expectedVersion: 7, BankRelationsChanges.None);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
    }

    [Fact]
    public async Task The_command_and_the_acknowledgement_survive_a_json_round_trip()
    {
        _directory.Seed(EntryId, Registered());
        _directory.Seed(ManagerId, RegisteredEntry(ManagerId, PartyType.Person));
        var command = Command(BankRelations, expectedVersion: 1, BankRelationsChanges.To(ManagedRelation));
        var acknowledged = (await UpdateBankRelationsHandler.HandleAsync(command, _directory, CancellationToken.None)).Value;

        JsonSerializer.Deserialize<UpdateBankRelations>(JsonSerializer.Serialize(command, _json), _json).ShouldBe(command);
        JsonSerializer.Deserialize<UpdateBankRelationsAcknowledged>(JsonSerializer.Serialize(acknowledged, _json), _json).ShouldBe(acknowledged);
    }

    private static UpdateBankRelations Command(BankRelationsId bankRelationsId, long expectedVersion, BankRelationsChanges changes) =>
        new(EntryId, bankRelationsId, expectedVersion, changes, IdempotencyKey.TryCreate("bank-relations-1").Value);

    private Task<Result<UpdateBankRelationsAcknowledged>> Update(BankRelationsId bankRelationsId, long expectedVersion, BankRelationsChanges changes) =>
        UpdateBankRelationsHandler.HandleAsync(Command(bankRelationsId, expectedVersion, changes), _directory, CancellationToken.None);
}
