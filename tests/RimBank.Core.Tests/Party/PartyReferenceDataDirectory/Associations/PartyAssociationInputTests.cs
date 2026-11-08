namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading what a caller sent for Associations/Register and Associations/Update (roadmap 1.11, ADR 0033): the fields
// RimBank does not record are refused first, then every invalid value is named in one party.association-invalid.
public sealed class PartyAssociationInputTests
{
    private static readonly string _associate = AssociationExamples.Associate.ToString();

    [Fact]
    public void Register_terms_read_the_associate_the_type_and_the_period() =>
        PartyAssociationTerms.Create(
                new PartyAssociationInput($" {_associate} ", "partyisparentofparty", new("2024-01-01", null)),
                [])
            .Value.ShouldBe(AssociationExamples.Terms());

    [Fact]
    public void Register_terms_without_a_period_or_with_a_blank_one_have_none()
    {
        PartyAssociationTerms.Create(new PartyAssociationInput(_associate, "PartyIsMarriedToParty", null), [])
            .Value.ValidityPeriod.ShouldBeNull();
        PartyAssociationTerms.Create(new PartyAssociationInput(_associate, "PartyIsMarriedToParty", new("", " ")), [])
            .Value.ValidityPeriod.ShouldBeNull();
    }

    [Fact]
    public void Register_terms_need_the_associate_and_the_type()
    {
        var error = PartyAssociationTerms.Create(new PartyAssociationInput(null, " ", null), []).Error;

        error.Code.ShouldBe("party.association-invalid");
        error.FieldErrors.Keys.ShouldBe(["AssociateReference.PartyReference", "AssociateType"], ignoreOrder: true);
    }

    [Fact]
    public void Every_invalid_value_is_named_with_its_bian_path()
    {
        var error = PartyAssociationTerms.Create(
                new PartyAssociationInput("not-an-id", "Cousin", new("2025-01-01", "2024-01-01")),
                [])
            .Error;

        error.Code.ShouldBe("party.association-invalid");
        error.FieldErrors.Keys.ShouldBe(
            ["AssociateReference.PartyReference", "AssociateType", "AssociationValidFromToDate.ToDateTime"],
            ignoreOrder: true);
    }

    [Fact]
    public void Fields_that_are_not_recorded_are_refused_before_the_values_are_read()
    {
        var error = PartyAssociationTerms.Create(
                new PartyAssociationInput(null, null, null),
                ["ProductInstanceReference", "AssociateReference.InvolvementReference", "ProductInstanceReference"])
            .Error;

        error.Code.ShouldBe("party.association-field-not-supported");
        error.FieldErrors.Keys.ShouldBe(["ProductInstanceReference", "AssociateReference.InvolvementReference"]);
    }

    [Fact]
    public void Update_changes_keep_what_is_left_out() =>
        PartyAssociationChanges.Create(new PartyAssociationInput(null, null, null), []).Value
            .ShouldBe(PartyAssociationChanges.None);

    [Fact]
    public void Update_changes_read_the_type_the_period_and_the_associate_named() =>
        PartyAssociationChanges.Create(
                new PartyAssociationInput(_associate, "PartyIsTrustedByParty", new("2024-01-01", "2025-12-31")),
                [])
            .Value.ShouldBe(new PartyAssociationChanges(
                AssociationExamples.Associate,
                PartyRelationshipType.PartyIsTrustedByParty,
                new AssociationValidityPeriodChange(AssociationExamples.EndedPeriod)));

    [Fact]
    public void A_blank_period_clears_it() =>
        PartyAssociationChanges.Create(new PartyAssociationInput(null, null, new(null, "")), []).Value.ValidityPeriod
            .ShouldBe(new AssociationValidityPeriodChange(null));

    [Fact]
    public void Neither_the_type_nor_the_associate_can_be_cleared() =>
        PartyAssociationChanges.Create(new PartyAssociationInput("", "", null), []).Error.FieldErrors.Keys
            .ShouldBe(["AssociateReference.PartyReference", "AssociateType"], ignoreOrder: true);

    [Fact]
    public void Update_changes_refuse_the_fields_that_are_not_recorded() =>
        PartyAssociationChanges.Create(new PartyAssociationInput(null, null, null), ["EmployeeReference"]).Error.Code
            .ShouldBe("party.association-field-not-supported");
}
