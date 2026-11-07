using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading the changes a Demographics Update asks for (roadmap 1.9): null keeps, blank clears, text is trimmed, and the
// ISO 20022 lengths are checked per BIAN field.
public sealed class DemographicsChangesTests
{
    [Fact]
    public void Missing_fields_keep_the_current_values() =>
        DemographicsChanges.Create(null, null, null, null, null).Value.ShouldBe(DemographicsChanges.None);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_field_clears_the_value(string blank)
    {
        var changes = DemographicsChanges.Create(blank, null, blank, blank, blank).Value;

        changes.ShouldBe(new DemographicsChanges("", null, "", "", ""));
    }

    [Fact]
    public void Text_is_trimmed() =>
        DemographicsChanges.Create("  B2 ", new Employment(" Contoso ", " Clerk "), " a ", " b ", " c ").Value
            .ShouldBe(new DemographicsChanges("B2", new Employment("Contoso", "Clerk"), "a", "b", "c"));

    [Fact]
    public void A_missing_or_blank_part_of_a_given_employment_is_unknown() =>
        DemographicsChanges.Create(null, new Employment("Contoso", "  "), null, null, null).Value.Employment
            .ShouldBe(new Employment("Contoso", null));

    [Fact]
    public void An_empty_employment_clears_it() =>
        DemographicsChanges.Create(null, new Employment(null, ""), null, null, null).Value.Employment
            .ShouldNotBeNull().IsEmpty.ShouldBeTrue();

    [Fact]
    public void Texts_at_their_maximum_length_are_accepted()
    {
        var shortText = new string('a', DemographicsChanges.ShortTextMaxLength);
        var longText = new string('b', DemographicsChanges.LongTextMaxLength);

        DemographicsChanges.Create(shortText, new Employment(shortText, shortText), longText, longText, longText).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Every_text_over_its_maximum_length_is_named_with_its_bian_field()
    {
        var shortText = new string('a', DemographicsChanges.ShortTextMaxLength + 1);
        var longText = new string('b', DemographicsChanges.LongTextMaxLength + 1);

        var error = DemographicsChanges.Create(shortText, new Employment(shortText, shortText), longText, longText, longText).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.demographics-invalid");
        error.FieldErrors.Keys.ShouldBe(
            [
                "Socio-EconomicClassification",
                "Employment.PartyReference",
                "Employment.InvolvementReference",
                "EmploymentHistory",
                "EducationHistory",
                "ServicingConstraints",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void The_length_is_checked_after_trimming() =>
        DemographicsChanges.Create($"  {new string('a', DemographicsChanges.ShortTextMaxLength)}  ", null, null, null, null)
            .IsSuccess.ShouldBeTrue();
}
