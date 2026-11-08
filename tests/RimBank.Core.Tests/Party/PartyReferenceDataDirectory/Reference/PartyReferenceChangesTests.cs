using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading the changes a Reference Update asks for (roadmap 1.10 and 1.18, ADR 0029): null keeps, blank clears, every
// invalid value is named with its BIAN field, and the fields RimBank does not record yet are refused with their phase.
public sealed class PartyReferenceChangesTests
{
    [Fact]
    public void Missing_values_keep_the_current_ones() =>
        PartyReferenceChanges.Create(PartyReferenceInput.None, []).Value.ShouldBe(PartyReferenceChanges.None);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_value_clears_it(string blank) =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    blank,
                    blank,
                    blank,
                    blank,
                    blank,
                    blank,
                    new PartyCountryInput(blank),
                    new PartyCountryInput(null),
                    new ResidencyStatusInput(blank, null)),
                [])
            .Value.ShouldBe(new PartyReferenceChanges(
                new PartyIdentifierChange<TaxIdentificationNumber>(null),
                new PartyIdentifierChange<NationalRegistrationNumber>(null),
                new PartyIdentifierChange<LegalEntityIdentifier>(null),
                new PartyReferenceChange<PartyFullLegalName?>(null),
                new PartyReferenceChange<PartyNameSalutation?>(null),
                new PartyReferenceChange<DateOnly?>(null),
                new PartyReferenceChange<CountryCode?>(null),
                new PartyReferenceChange<CountryCode?>(null),
                new PartyReferenceChange<ResidencyStatus?>(null)));

    [Fact]
    public void Given_identifiers_are_read_into_their_value_objects() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(" FR12345678901 ", "1 85 05 78 006 084 36", Lei.ToLowerInvariant()),
                [])
            .Value.ShouldBe(ReferenceChangesTo(Identifiers));

    [Fact]
    public void Given_names_and_residency_are_read_into_their_value_objects() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    " FR12345678901 ",
                    "1 85 05 78 006 084 36",
                    Lei,
                    "  Jeanne Marie Dupont ",
                    "Ms",
                    "1990-05-17",
                    new PartyCountryInput(" fr "),
                    new PartyCountryInput("BE"),
                    new ResidencyStatusInput(" nonresident ", "2024-01-01T00:00:00Z")),
                [])
            .Value.ShouldBe(ReferenceChangesTo(FullReferenceDetails));

    [Theory]
    [InlineData("1990-05-17")]
    [InlineData("1990-05-17T00:00:00")]
    [InlineData("1990-05-17T00:00:00Z")]
    [InlineData("1990-05-17T23:30:00-05:00")] // the date as written, not converted to UTC
    [InlineData("1990-05-17T01:00:00.123+14:00")]
    [InlineData(" 1990-05-17 ")]
    public void A_date_of_birth_is_the_calendar_date_as_written(string text) =>
        PartyReferenceChanges.Create(new PartyReferenceInput(DateofBirth: text), []).Value.DateOfBirth
            .ShouldBe(new PartyReferenceChange<DateOnly?>(new DateOnly(1990, 5, 17)));

    [Theory]
    [InlineData("17/05/1990")]
    [InlineData("1990-13-01")]
    [InlineData("1990-02-30")]
    [InlineData("19900517")]
    [InlineData("yesterday")]
    public void A_date_of_birth_that_is_not_an_iso_date_is_invalid(string text) =>
        PartyReferenceChanges.Create(new PartyReferenceInput(DateofBirth: text), []).Error
            .FieldErrors.ShouldNotBeNull().Keys.ShouldBe(["DateofBirth"]);

    [Theory]
    [InlineData("1899-12-31")]
    [InlineData("1850-06-01")]
    [InlineData("0001-01-01")]
    [InlineData("1899-12-31T23:59:59Z")]
    public void A_date_of_birth_before_1900_is_invalid(string text)
    {
        var error = PartyReferenceChanges.Create(new PartyReferenceInput(DateofBirth: text), []).Error;

        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.ShouldNotBeNull()["DateofBirth"].ShouldBe(["The date of birth must not be before 1900-01-01."]);
    }

    [Fact]
    public void The_first_of_january_1900_is_the_earliest_date_of_birth_accepted()
    {
        PartyReferenceChanges.EarliestDateOfBirth.ShouldBe(new DateOnly(1900, 1, 1));
        PartyReferenceChanges.Create(new PartyReferenceInput(DateofBirth: "1900-01-01"), []).Value.DateOfBirth
            .ShouldBe(new PartyReferenceChange<DateOnly?>(new DateOnly(1900, 1, 1)));
    }

    [Theory]
    [InlineData("resident", ResidencyStatusCode.Resident)]
    [InlineData("Resident", ResidencyStatusCode.Resident)]
    [InlineData("NONRESIDENT", ResidencyStatusCode.NonResident)]
    [InlineData(" NonResident ", ResidencyStatusCode.NonResident)]
    public void A_residency_code_is_matched_without_regard_to_case(string text, ResidencyStatusCode code) =>
        PartyReferenceChanges.Create(new PartyReferenceInput(ResidencyStatus: new ResidencyStatusInput(text, null)), []).Value
            .ResidencyStatus.ShouldBe(new PartyReferenceChange<ResidencyStatus?>(new ResidencyStatus(code, null)));

    [Fact]
    public void A_status_date_time_keeps_its_offset() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(ResidencyStatus: new ResidencyStatusInput("Resident", "2024-03-01T09:30:00+01:00")),
                [])
            .Value.ResidencyStatus.ShouldNotBeNull().Value.ShouldNotBeNull().StatusDateTime
            .ShouldBe(new DateTimeOffset(2024, 3, 1, 9, 30, 0, TimeSpan.FromHours(1)));

    [Fact]
    public void Every_invalid_value_is_named_with_its_bian_field()
    {
        var error = PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    new string('1', 36),
                    new string('2', 36),
                    "5493001KJTIIGC8Y1R13",
                    new string('N', 141),
                    new string('S', 36),
                    "not a date",
                    new PartyCountryInput("ZZ"),
                    new PartyCountryInput("FRA"),
                    new ResidencyStatusInput("Expat", "tomorrow")),
                [])
            .Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.ShouldNotBeNull().Keys.ShouldBe(
            [
                "GovernmentIssuedIdentityReference.TaxIdentificationNumber",
                "GovernmentIssuedIdentityReference.NationalRegistrationNumber",
                "PartyLegalEntityReference.Identification",
                "PartyFullLegalName",
                "PartyNameSalutation",
                "DateofBirth",
                "Nationality.Code",
                "PartyCountryOfResidence.Code",
                "ResidencyStatus.StatusDescription",
                "ResidencyStatus.StatusDateTime",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Names_at_their_maximum_length_are_accepted()
    {
        var changes = PartyReferenceChanges.Create(
                new PartyReferenceInput(PartyFullLegalName: new string('N', 140), PartyNameSalutation: new string('S', 35)),
                [])
            .Value;

        changes.PartyFullLegalName.ShouldNotBeNull().Value.ShouldNotBeNull().Value.Length.ShouldBe(140);
        changes.PartyNameSalutation.ShouldNotBeNull().Value.ShouldNotBeNull().Value.Length.ShouldBe(35);
    }

    [Fact]
    public void A_status_date_time_without_a_code_is_invalid() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(ResidencyStatus: new ResidencyStatusInput(null, "2024-01-01T00:00:00Z")),
                [])
            .Error.FieldErrors.ShouldNotBeNull().Keys.ShouldBe(["ResidencyStatus.StatusDescription"]);

    [Fact]
    public void A_staged_field_is_refused_with_the_phase_that_adds_it()
    {
        var error = PartyReferenceChanges.Create(PartyReferenceInput.None, ["eMailAddress", "PoliticalExposureType"]).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.reference-field-not-supported");
        var fieldErrors = error.FieldErrors.ShouldNotBeNull();
        fieldErrors["eMailAddress"].ShouldHaveSingleItem().ShouldContain("1.19");
        fieldErrors["PoliticalExposureType"].ShouldHaveSingleItem().ShouldContain("1.20");
    }

    [Theory]
    [InlineData("GovernmentIssuedIdentityReference.OtherIdentification")]
    [InlineData("Nationality.Citizen")]
    [InlineData("PartyCountryOfResidence.Tax")]
    [InlineData("ResidencyStatus.StatusReason")]
    public void A_field_no_phase_adds_is_refused_without_a_phase(string field) =>
        PartyReferenceChanges.Create(PartyReferenceInput.None, [field]).Error
            .FieldErrors.ShouldNotBeNull()[field]
            .ShouldBe(["RimBank does not record this field."]);

    [Fact]
    public void Unsupported_fields_are_refused_before_the_values_are_checked() =>
        PartyReferenceChanges.Create(new PartyReferenceInput(new string('1', 36), DateofBirth: "garbage"), ["eMailAddress"]).Error.Code
            .ShouldBe("party.reference-field-not-supported");

    [Fact]
    public void Every_bian_reference_field_is_either_recorded_or_staged()
    {
        // The 24 top-level fields of BIAN UpdateReference (PartyReferenceDataDirectory.yaml): two are recorded from 1.10
        // and six from 1.18.
        PartyReferenceFields.Staged.Count.ShouldBe(16);
        PartyReferenceFields.Staged.Keys.ShouldNotContain(PartyReferenceFields.GovernmentIssuedIdentityReference);
        PartyReferenceFields.Staged.Keys.ShouldNotContain(PartyReferenceFields.PartyLegalEntityReference);
        string[] recordedIn118 =
        [
            PartyReferenceFields.PartyFullLegalName,
            PartyReferenceFields.PartyNameSalutation,
            PartyReferenceFields.DateofBirth,
            PartyReferenceFields.Nationality,
            PartyReferenceFields.PartyCountryOfResidence,
            PartyReferenceFields.ResidencyStatus,
        ];
        PartyReferenceFields.Staged.Keys.ShouldNotContain(field => recordedIn118.Contains(field));
        PartyReferenceFields.Staged.Values.Distinct().Order().ShouldBe(["1.19", "1.20"]);
    }
}
