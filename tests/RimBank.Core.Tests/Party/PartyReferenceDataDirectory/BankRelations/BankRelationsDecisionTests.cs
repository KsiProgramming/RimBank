using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.BankRelationsExamples;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The BankRelations Update decision (roadmap 1.12, ADR 0035): checked on the resulting BankRelations. An employee or unit
// needs a type, the relationship does not end before it starts, and the employee or unit to check (set, or kept under a
// new type) is another registered entry of the party type the relation type needs, as the handler loaded it.
public sealed class BankRelationsDecisionTests
{
    [Theory]
    [InlineData(PartyType.Person)]
    [InlineData(PartyType.Organisation)]
    public void Any_party_records_its_relationship_manager(PartyType partyType) =>
        EntrySpecification.Given(Registered(partyType))
            .When(UpdateRelations(BankRelationsChanges.To(ManagedRelation), PartyType.Person))
            .Then(RelationsUpdated(ManagedRelation));

    [Fact]
    public void A_branch_services_the_party() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(
                new BankRelationsChanges(new(BankRelationType.ServicingBranch), new(BranchId)),
                PartyType.Organisation))
            .Then(RelationsUpdated(BankRelationsDetails.Empty with { Type = BankRelationType.ServicingBranch, BusinessUnitEmployee = BranchId }));

    [Fact]
    public void A_partial_update_keeps_what_it_does_not_send_without_checking_the_reference_again() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(new BankRelationsChanges(EndDate: new(EndDate), LifecycleStatus: new(BankRelationLifecycleStatus.Ended))))
            .Then(RelationsUpdated(ManagedRelation with { EndDate = EndDate, LifecycleStatus = BankRelationLifecycleStatus.Ended }));

    [Fact]
    public void An_update_with_no_change_is_still_recorded() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(BankRelationsChanges.None))
            .Then(RelationsUpdated(ManagedRelation));

    [Fact]
    public void Clearing_every_part_leaves_empty_bank_relations() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(BankRelationsChanges.To(BankRelationsDetails.Empty)))
            .Then(RelationsUpdated(BankRelationsDetails.Empty));

    [Fact]
    public void Dates_and_status_need_no_type() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(new BankRelationsChanges(StartDate: new(StartDate), LifecycleStatus: new(BankRelationLifecycleStatus.Suspended))))
            .Then(RelationsUpdated(BankRelationsDetails.Empty with { StartDate = StartDate, LifecycleStatus = BankRelationLifecycleStatus.Suspended }));

    [Fact]
    public void An_unknown_entry_is_not_found() =>
        EntrySpecification.Given()
            .When(UpdateRelations(BankRelationsChanges.None))
            .ThenFails(ErrorKind.NotFound, "party.party-not-found");

    [Fact]
    public void A_foreign_bank_relations_id_is_not_found() =>
        EntrySpecification.Given(Registered())
            .When(new UpdateBankRelationsDetails(BankRelationsId.From(Guid.CreateVersion7(OpenDate.AddDays(1))), BankRelationsChanges.None))
            .ThenFails(ErrorKind.NotFound, "party.behavior-qualifier-not-found");

    [Fact]
    public void A_reference_without_a_type_is_invalid() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(new BankRelationsChanges(BusinessUnitEmployee: new(ManagerId)), PartyType.Person))
            .ThenFails(ErrorKind.Validation, "party.bank-relations-invalid")
            .FieldErrors.Keys.ShouldBe(["BankRelationType"]);

    [Fact]
    public void Clearing_the_type_while_the_reference_is_kept_is_invalid() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(new BankRelationsChanges(Type: new(null))))
            .ThenFails(ErrorKind.Validation, "party.bank-relations-invalid");

    [Fact]
    public void A_relationship_that_ends_before_it_starts_is_invalid_even_when_the_start_is_kept() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(new BankRelationsChanges(EndDate: new(StartDate.AddDays(-1)))))
            .ThenFails(ErrorKind.Validation, "party.bank-relations-invalid")
            .FieldErrors.Keys.ShouldBe(["BankRelationReference.RelationshipEndDate.DateTimeContent"]);

    [Fact]
    public void A_relationship_may_start_and_end_on_the_same_day() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(new BankRelationsChanges(StartDate: new(StartDate), EndDate: new(StartDate))))
            .Then(RelationsUpdated(BankRelationsDetails.Empty with { StartDate = StartDate, EndDate = StartDate }));

    [Fact]
    public void The_entry_itself_as_its_employee_is_refused_naming_the_path() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(new BankRelationsChanges(new(BankRelationType.RelationshipManager), new(EntryId))))
            .ThenFails(ErrorKind.BusinessRule, "party.self-reference")
            .Message.ShouldStartWith("BusinessUnitEmployeeReference.PartyReference");

    [Fact]
    public void An_employee_that_is_not_registered_is_refused_naming_the_path() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(BankRelationsChanges.To(ManagedRelation), referencedPartyType: null))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-not-registered")
            .Message.ShouldStartWith("BusinessUnitEmployeeReference.PartyReference");

    [Fact]
    public void An_organisation_as_relationship_manager_is_a_type_mismatch() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(BankRelationsChanges.To(ManagedRelation), PartyType.Organisation))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-type-mismatch")
            .Message.ShouldBe("BusinessUnitEmployeeReference.PartyReference must name a party of type Person.");

    [Fact]
    public void A_person_as_servicing_branch_is_a_type_mismatch() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(new BankRelationsChanges(new(BankRelationType.ServicingBranch), new(ManagerId)), PartyType.Person))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-type-mismatch")
            .Message.ShouldBe("BusinessUnitEmployeeReference.PartyReference must name a party of type Organisation.");

    [Fact]
    public void A_new_type_is_checked_against_the_kept_reference() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(new BankRelationsChanges(Type: new(BankRelationType.ServicingBusinessUnit)), PartyType.Person))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-type-mismatch");

    [Fact]
    public void A_new_type_that_fits_the_kept_reference_is_accepted() =>
        EntrySpecification.Given(Registered(), RelationsUpdated(ManagedRelation))
            .When(UpdateRelations(new BankRelationsChanges(Type: new(BankRelationType.AccountOfficer)), PartyType.Person))
            .Then(RelationsUpdated(ManagedRelation with { Type = BankRelationType.AccountOfficer }));

    [Fact]
    public void The_value_rules_are_checked_before_the_reference() =>
        EntrySpecification.Given(Registered())
            .When(UpdateRelations(
                new BankRelationsChanges(BusinessUnitEmployee: new(EntryId), StartDate: new(EndDate), EndDate: new(StartDate))))
            .ThenFails(ErrorKind.Validation, "party.bank-relations-invalid");

    [Fact]
    public void An_update_of_the_bank_relations_changes_no_other_section()
    {
        var state = new PartyReferenceDataDirectoryEntryEvent[] { Registered(), ReferenceUpdated(), RelationsUpdated(ManagedRelation) }
            .Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        state.CurrentReference.ShouldBe(ReferenceDetails);
        state.CurrentBankRelations.ShouldBe(ManagedRelation);
        PartyReferenceDataDirectoryEntry.Initial.CurrentBankRelations.ShouldBe(BankRelationsDetails.Empty);
    }
}
