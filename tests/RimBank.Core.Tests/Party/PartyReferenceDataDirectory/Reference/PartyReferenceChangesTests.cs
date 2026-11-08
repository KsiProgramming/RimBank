using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading the changes a Reference Update asks for (roadmap 1.10, ADR 0029): null keeps, blank clears, every invalid
// identifier is named with its BIAN field, and the fields RimBank does not record yet are refused with their phase.
public sealed class PartyReferenceChangesTests
{
    [Fact]
    public void Missing_identifiers_keep_the_current_ones() =>
        PartyReferenceChanges.Create(null, null, null, []).Value.ShouldBe(PartyReferenceChanges.None);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_identifier_clears_it(string blank) =>
        PartyReferenceChanges.Create(blank, blank, blank, []).Value.ShouldBe(new PartyReferenceChanges(
            new PartyIdentifierChange<TaxIdentificationNumber>(null),
            new PartyIdentifierChange<NationalRegistrationNumber>(null),
            new PartyIdentifierChange<LegalEntityIdentifier>(null)));

    [Fact]
    public void Given_identifiers_are_read_into_their_value_objects() =>
        PartyReferenceChanges.Create(" FR12345678901 ", "1 85 05 78 006 084 36", Lei.ToLowerInvariant(), []).Value
            .ShouldBe(ReferenceChangesTo(Identifiers));

    [Fact]
    public void Every_invalid_identifier_is_named_with_its_bian_field()
    {
        var error = PartyReferenceChanges.Create(new string('1', 36), new string('2', 36), "5493001KJTIIGC8Y1R13", []).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.ShouldNotBeNull().Keys.ShouldBe(
            [
                "GovernmentIssuedIdentityReference.TaxIdentificationNumber",
                "GovernmentIssuedIdentityReference.NationalRegistrationNumber",
                "PartyLegalEntityReference.Identification",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void A_staged_field_is_refused_with_the_phase_that_adds_it()
    {
        var error = PartyReferenceChanges.Create(null, null, null, ["DateofBirth", "eMailAddress", "PoliticalExposureType"]).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.reference-field-not-supported");
        var fieldErrors = error.FieldErrors.ShouldNotBeNull();
        fieldErrors["DateofBirth"].ShouldHaveSingleItem().ShouldContain("1.18");
        fieldErrors["eMailAddress"].ShouldHaveSingleItem().ShouldContain("1.19");
        fieldErrors["PoliticalExposureType"].ShouldHaveSingleItem().ShouldContain("1.20");
    }

    [Fact]
    public void A_field_no_phase_adds_is_refused_without_a_phase() =>
        PartyReferenceChanges.Create(null, null, null, ["GovernmentIssuedIdentityReference.OtherIdentification"]).Error
            .FieldErrors.ShouldNotBeNull()["GovernmentIssuedIdentityReference.OtherIdentification"]
            .ShouldBe(["RimBank does not record this field."]);

    [Fact]
    public void Unsupported_fields_are_refused_before_the_identifiers_are_checked() =>
        PartyReferenceChanges.Create(new string('1', 36), null, null, ["DateofBirth"]).Error.Code
            .ShouldBe("party.reference-field-not-supported");

    [Fact]
    public void Every_bian_reference_field_is_either_recorded_or_staged()
    {
        // The 24 top-level fields of BIAN UpdateReference (PartyReferenceDataDirectory.yaml): two are recorded from 1.10.
        PartyReferenceFields.Staged.Count.ShouldBe(22);
        PartyReferenceFields.Staged.Keys.ShouldNotContain(PartyReferenceFields.GovernmentIssuedIdentityReference);
        PartyReferenceFields.Staged.Keys.ShouldNotContain(PartyReferenceFields.PartyLegalEntityReference);
        PartyReferenceFields.Staged.Values.Distinct().Order().ShouldBe(["1.18", "1.19", "1.20"]);
    }
}
