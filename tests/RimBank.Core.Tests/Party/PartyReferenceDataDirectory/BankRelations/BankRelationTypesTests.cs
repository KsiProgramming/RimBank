using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The bank relation types and lifecycle statuses (roadmap 1.12, ADR 0035): read in any case, written as their names, and
// each type names the party type of the employee or unit it references.
public sealed class BankRelationTypesTests
{
    [Theory]
    [InlineData("RelationshipManager", BankRelationType.RelationshipManager)]
    [InlineData(" accountofficer ", BankRelationType.AccountOfficer)]
    [InlineData("SERVICINGBRANCH", BankRelationType.ServicingBranch)]
    [InlineData("ServicingBusinessUnit", BankRelationType.ServicingBusinessUnit)]
    public void A_type_is_read_in_any_case(string text, BankRelationType expected) =>
        BankRelationTypes.Read(text).Value.ShouldBe(expected);

    [Theory]
    [InlineData("Relationship Manager")]
    [InlineData("1")]
    [InlineData("Customer")]
    public void Anything_else_is_invalid_naming_the_field(string text)
    {
        var error = BankRelationTypes.Read(text).Error;

        error.Code.ShouldBe("party.bank-relations-invalid");
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors.Keys.ShouldBe(["BankRelationType"]);
    }

    [Theory]
    [InlineData(BankRelationType.RelationshipManager, PartyType.Person)]
    [InlineData(BankRelationType.AccountOfficer, PartyType.Person)]
    [InlineData(BankRelationType.ServicingBranch, PartyType.Organisation)]
    [InlineData(BankRelationType.ServicingBusinessUnit, PartyType.Organisation)]
    public void An_employee_s_role_references_a_person_and_a_unit_s_an_organisation(BankRelationType type, PartyType expected) =>
        type.ReferencedPartyType().ShouldBe(expected);

    [Fact]
    public void Every_type_is_written_as_its_name() =>
        Enum.GetValues<BankRelationType>().Select(type => type.ToBianName())
            .ShouldBe(["RelationshipManager", "AccountOfficer", "ServicingBranch", "ServicingBusinessUnit"]);

    [Fact]
    public void An_undefined_type_has_no_name() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ((BankRelationType)99).ToBianName());

    [Theory]
    [InlineData("active", BankRelationLifecycleStatus.Active)]
    [InlineData(" Suspended ", BankRelationLifecycleStatus.Suspended)]
    [InlineData("ENDED", BankRelationLifecycleStatus.Ended)]
    public void A_lifecycle_status_is_read_in_any_case(string text, BankRelationLifecycleStatus expected) =>
        BankRelationLifecycleStatuses.Read(text).Value.ShouldBe(expected);

    [Fact]
    public void An_unknown_lifecycle_status_is_invalid_naming_its_path() =>
        BankRelationLifecycleStatuses.Read("Closed").Error.FieldErrors.Keys
            .ShouldBe(["BankRelationReference.RelationshipLifecycleStatus.StatusDescription"]);

    [Fact]
    public void Every_lifecycle_status_is_written_as_its_name() =>
        Enum.GetValues<BankRelationLifecycleStatus>().Select(status => status.ToBianName()).ShouldBe(["Active", "Suspended", "Ended"]);
}
