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

    [Fact]
    public async Task The_referenced_entries_are_loaded_from_the_write_side_and_recorded()
    {
        _directory.Seed(EntryId, Registered(PartyType.Organisation));
        _directory.Seed(CompanyId, RegisteredEntry(CompanyId, PartyType.Organisation));
        _directory.Seed(OfficerId, RegisteredEntry(OfficerId, PartyType.Person));

        var acknowledged = (await Update(Reference, expectedVersion: 1, CorporateReferenceChangesTo(CorporateReferences))).Value;

        acknowledged.Reference.CorporateReferences.ShouldBe(CorporateReferences);
        _directory.StreamOf(EntryId).Count.ShouldBe(2);
        _directory.StreamOf(CompanyId).ShouldBe([RegisteredEntry(CompanyId, PartyType.Organisation)]);
    }

    [Fact]
    public async Task A_referenced_entry_that_is_not_registered_is_refused_and_nothing_is_appended()
    {
        _directory.Seed(EntryId, Registered(PartyType.Organisation));

        var result = await Update(Reference, expectedVersion: 1, PartyReferenceChanges.None with { CorporateCustomer = new(CompanyId) });

        result.Error.Code.ShouldBe("party.referenced-party-not-registered");
        result.Error.Kind.ShouldBe(ErrorKind.BusinessRule);
        _directory.StreamOf(EntryId).ShouldBe([Registered(PartyType.Organisation)]);
    }

    [Fact]
    public async Task An_association_id_given_as_a_reference_is_not_registered_never_a_failure()
    {
        _directory.Seed(EntryId, Registered(PartyType.Organisation));
        var associationId = PartyAssociationId.From(Guid.CreateVersion7(OpenDate.AddMinutes(9)));
        _directory.Seed(associationId, new PartyAssociationRegistered(
            associationId,
            EntryId,
            CompanyId,
            PartyRelationshipType.PartyIsParentOfParty,
            null));

        var result = await Update(
            Reference,
            expectedVersion: 1,
            PartyReferenceChanges.None with { LegalEntity = new(PartyReferenceDataDirectoryEntryId.From(associationId.Value)) });

        result.Error.Code.ShouldBe("party.referenced-party-not-registered");
    }

    [Fact]
    public async Task A_reference_to_the_entry_itself_is_refused()
    {
        _directory.Seed(EntryId, Registered(PartyType.Organisation));

        var result = await Update(Reference, expectedVersion: 1, PartyReferenceChanges.None with { CorporateCustomer = new(EntryId) });

        result.Error.Code.ShouldBe("party.self-reference");
    }

    [Fact]
    public async Task The_party_types_loaded_are_the_ones_the_decision_checks()
    {
        _directory.Seed(EntryId, Registered(PartyType.Organisation));
        _directory.Seed(OfficerId, RegisteredEntry(OfficerId, PartyType.Organisation), new PartyReferenceDataDirectoryEntryUpdated(OfficerId, PartyType.Person, RefreshDate));
        _directory.Seed(CompanyId, RegisteredEntry(CompanyId, PartyType.Organisation));

        (await Update(Reference, expectedVersion: 1, PartyReferenceChanges.None with { CompanyOfficer = new(OfficerId) })).IsSuccess.ShouldBeTrue();
        (await Update(Reference, expectedVersion: 2, PartyReferenceChanges.None with { CompanyOfficer = new(CompanyId) }))
            .Error.Code.ShouldBe("party.referenced-party-type-mismatch");
    }

    [Fact]
    public async Task A_person_s_entry_cannot_record_corporate_references()
    {
        _directory.Seed(EntryId, Registered(PartyType.Person));
        _directory.Seed(CompanyId, RegisteredEntry(CompanyId, PartyType.Organisation));

        var result = await Update(Reference, expectedVersion: 1, PartyReferenceChanges.None with { CorporateCustomer = new(CompanyId) });

        result.Error.Code.ShouldBe("party.corporate-references-need-organisation");
    }

    [Fact]
    public async Task A_kept_reference_is_not_checked_again()
    {
        // The officer was recorded when it was registered; the stream here holds a reference to an entry the directory does
        // not know, so the Update could only pass if the kept reference is not loaded again.
        _directory.Seed(
            EntryId,
            Registered(PartyType.Organisation),
            ReferenceUpdated(new PartyReferenceDetails(PartyIdentifiers.Empty, CorporateReferences: CorporateReferences)));

        var result = await Update(Reference, expectedVersion: 2, PartyReferenceChanges.None with { PartyFullLegalName = new(Names.FullLegalName) });

        result.Value.Reference.CorporateReferences.ShouldBe(CorporateReferences);
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
