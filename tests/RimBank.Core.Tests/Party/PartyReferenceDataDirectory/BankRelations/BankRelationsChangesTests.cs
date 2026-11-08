using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.BankRelationsExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading a BankRelations Update (roadmap 1.12, ADR 0035): unsupported paths are refused first, every invalid value is
// named by its BIAN path, null keeps, blank clears, and an empty BankRelationReference clears the dates and the status.
public sealed class BankRelationsChangesTests
{
    private static readonly string[] _none = [];

    [Fact]
    public void Every_recorded_value_is_read()
    {
        var changes = BankRelationsChanges.Create(
            new BankRelationsInput(
                " relationshipmanager ",
                ManagerId.ToString(),
                new BankRelationReferenceInput("2024-01-01", "2026-06-30T02:00:00+02:00", "active")),
            _none).Value;

        changes.ShouldBe(new BankRelationsChanges(
            new(BankRelationType.RelationshipManager),
            new(ManagerId),
            new(StartDate),
            new(EndDate),
            new(BankRelationLifecycleStatus.Active)));
    }

    [Fact]
    public void Nothing_sent_changes_nothing() =>
        BankRelationsChanges.Create(new BankRelationsInput(null, null, null), _none).Value.ShouldBe(BankRelationsChanges.None);

    [Fact]
    public void Blank_values_clear_their_parts() =>
        BankRelationsChanges.Create(new BankRelationsInput(" ", "", new BankRelationReferenceInput("", null, " ")), _none).Value
            .ShouldBe(new BankRelationsChanges(new(null), new(null), new(null), null, new(null)));

    [Fact]
    public void An_empty_bank_relation_reference_clears_the_dates_and_the_status() =>
        BankRelationsChanges.Create(new BankRelationsInput(null, null, new BankRelationReferenceInput(null, null, null)), _none).Value
            .ShouldBe(new BankRelationsChanges(StartDate: new(null), EndDate: new(null), LifecycleStatus: new(null)));

    [Fact]
    public void Every_invalid_value_is_named_by_its_path()
    {
        var error = BankRelationsChanges.Create(
            new BankRelationsInput(
                "Customer",
                "BRANCH-1",
                new BankRelationReferenceInput("01/01/2024", "tomorrow", "Closed")),
            _none).Error;

        error.Code.ShouldBe("party.bank-relations-invalid");
        error.FieldErrors.Keys.ShouldBe(
        [
            "BankRelationType",
            "BusinessUnitEmployeeReference.PartyReference",
            "BankRelationReference.RelationshipStartDate.DateTimeContent",
            "BankRelationReference.RelationshipEndDate.DateTimeContent",
            "BankRelationReference.RelationshipLifecycleStatus.StatusDescription",
        ],
            ignoreOrder: true);
    }

    [Fact]
    public void A_party_reference_that_is_not_a_version_7_id_is_invalid() =>
        BankRelationsChanges.Create(new BankRelationsInput(null, Guid.NewGuid().ToString(), null), _none).Error.FieldErrors.Keys
            .ShouldBe(["BusinessUnitEmployeeReference.PartyReference"]);

    [Fact]
    public void Unsupported_paths_are_refused_before_the_values_are_read()
    {
        var error = BankRelationsChanges.Create(
            new BankRelationsInput("Customer", null, null),
            ["BankRelationReference.RelationshipIdentification", "BusinessUnitEmployeeReference.InvolvementReference"]).Error;

        error.Code.ShouldBe("party.bank-relations-field-not-supported");
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors.Keys.ShouldBe(
            ["BankRelationReference.RelationshipIdentification", "BusinessUnitEmployeeReference.InvolvementReference"],
            ignoreOrder: true);
    }

    [Fact]
    public void The_changes_to_some_details_give_those_details_whatever_the_current_ones() =>
        new BankRelationsDetails(BankRelationType.ServicingBranch, BranchId, null, EndDate, BankRelationLifecycleStatus.Ended)
            .Apply(BankRelationsChanges.To(ManagedRelation))
            .ShouldBe(ManagedRelation);

    [Fact]
    public void Only_a_change_of_the_reference_or_the_type_asks_for_a_check()
    {
        ManagedRelation.ReferenceToCheck(BankRelationsChanges.None).ShouldBeNull();
        ManagedRelation.ReferenceToCheck(new BankRelationsChanges(LifecycleStatus: new(BankRelationLifecycleStatus.Ended))).ShouldBeNull();
        ManagedRelation.ReferenceToCheck(new BankRelationsChanges(Type: new(BankRelationType.ServicingBranch))).ShouldBe(ManagerId);
        ManagedRelation.ReferenceToCheck(new BankRelationsChanges(BusinessUnitEmployee: new(BranchId))).ShouldBe(BranchId);
        ManagedRelation.ReferenceToCheck(new BankRelationsChanges(BusinessUnitEmployee: new(null))).ShouldBeNull();
    }
}
