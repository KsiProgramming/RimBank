using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Reading the changes a Reference Update asks for (roadmap 1.10 to 1.21, ADR 0029, ADR 0031, ADR 0032, ADR 0034): null
// keeps, blank clears, every invalid value is named with its BIAN field, and the fields RimBank does not record are refused.
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
    public void Given_corporate_and_officer_references_are_read_into_entry_ids_and_the_role() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    CorporateCustomerReference: $" {CompanyId} ",
                    CorporateCustomerLegalEntityReference: CompanyId.ToString(),
                    CompanyOfficerReference: OfficerId.ToString(),
                    CompanyOfficerRole: "  Director "),
                [])
            .Value.ShouldBe(CorporateReferenceChangesTo(CorporateReferences));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_reference_or_role_clears_it(string blank) =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    CorporateCustomerReference: blank,
                    CorporateCustomerLegalEntityReference: blank,
                    CompanyOfficerReference: blank,
                    CompanyOfficerRole: blank),
                [])
            .Value.ShouldBe(PartyReferenceChanges.None with
            {
                CorporateCustomer = new(null),
                LegalEntity = new(null),
                CompanyOfficer = new(null),
                CompanyOfficerRole = new(null),
            });

    [Fact]
    public void Every_invalid_reference_and_role_is_named_with_its_bian_path()
    {
        var error = PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    CorporateCustomerReference: "ACME-1",
                    CorporateCustomerLegalEntityReference: Guid.Empty.ToString(),
                    CompanyOfficerReference: Guid.NewGuid().ToString(), // version 4: not an id the directory issues
                    CompanyOfficerRole: new string('r', 36)),
                [])
            .Error;

        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.Keys.Order(StringComparer.Ordinal).ShouldBe(
        [
            "CompanyOfficerReference.PartyReference",
            "CompanyOfficerRole",
            "CorporateCustomerLegalEntityReference.PartyReference",
            "CorporateCustomerReference.PartyReference",
        ]);
    }

    [Fact]
    public void The_referenced_entries_are_the_ones_the_changes_set_with_their_bian_paths()
    {
        var changes = PartyReferenceChanges.None with
        {
            CorporateCustomer = new(CompanyId),
            LegalEntity = new(null),
            CompanyOfficer = new(OfficerId),
        };

        changes.ReferencedEntries().ShouldBe(
        [
            ("CorporateCustomerReference.PartyReference", CompanyId),
            ("CompanyOfficerReference.PartyReference", OfficerId),
        ]);
        PartyReferenceChanges.None.ReferencedEntries().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("GovernmentIssuedIdentityReference.OtherIdentification")]
    [InlineData("Nationality.Citizen")]
    [InlineData("PartyCountryOfResidence.Tax")]
    [InlineData("ResidencyStatus.StatusReason")]
    [InlineData("ResidentialAddress.AddressType")]
    [InlineData("PartyAddress.ValidityPeriod")]
    [InlineData("CorporateAddress.Country.Citizen")]
    [InlineData("eMailAddress.Identification")]
    [InlineData("CellPhoneNumber.PhoneNumber")]
    [InlineData("SocialNetworkContacts.BICAddress")]
    [InlineData("GovernmentIssuedDocumentDetails.PartyReference")]
    [InlineData("GovernmentIssuedDocumentDetails.DocumentReference.DocumentStatus")]
    [InlineData("GovernmentIssuedDocumentDetails.DocumentReference.DocumentIdentification.IdentifierIssuerReference")]
    [InlineData("GovernmentIssuedDocumentDetails.DocumentReference.DocumentValidityPeriod.ToDateTime.TimeZoneCode")]
    [InlineData("DocumentDirectoryEntryInstanceReference.DirectoryEntryStatus")]
    [InlineData("CorporateCustomerReference.InvolvementReference")]
    [InlineData("CorporateCustomerLegalEntityReference.InvolvementReference")]
    [InlineData("CompanyOfficerReference.InvolvementReference")]
    [InlineData("CompanyOfficerRole.Identification")]
    public void A_field_no_phase_adds_is_refused_without_a_phase(string field) =>
        PartyReferenceChanges.Create(PartyReferenceInput.None, [field]).Error
            .FieldErrors.ShouldNotBeNull()[field]
            .ShouldBe(["RimBank does not record this field."]);

    [Fact]
    public void Unsupported_fields_are_refused_before_the_values_are_checked() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(new string('1', 36), DateofBirth: "garbage"),
                ["CompanyOfficerReference.InvolvementReference"])
            .Error.Code
            .ShouldBe("party.reference-field-not-supported");

    [Fact]
    public void Every_bian_reference_field_is_recorded()
    {
        // The 24 top-level fields of BIAN UpdateReference (PartyReferenceDataDirectory.yaml): two are recorded from 1.10,
        // six from 1.18, six from 1.19, six from 1.20 and the four references to other parties from 1.21 (ADR 0034).
        string[] recorded =
        [
            PartyReferenceFields.GovernmentIssuedIdentityReference,
            PartyReferenceFields.PartyLegalEntityReference,
            PartyReferenceFields.PartyFullLegalName,
            PartyReferenceFields.PartyNameSalutation,
            PartyReferenceFields.DateofBirth,
            PartyReferenceFields.Nationality,
            PartyReferenceFields.PartyCountryOfResidence,
            PartyReferenceFields.ResidencyStatus,
            PartyReferenceFields.ResidentialAddress,
            PartyReferenceFields.PartyAddress,
            PartyReferenceFields.CorporateAddress,
            PartyReferenceFields.EMailAddress,
            PartyReferenceFields.CellPhoneNumber,
            PartyReferenceFields.SocialNetworkContacts,
            PartyReferenceFields.PartyLegalStructure,
            PartyReferenceFields.PoliticalExposureType,
            PartyReferenceFields.PoliticalExposureDescriptionRecord,
            PartyReferenceFields.CustomerSinceDate,
            PartyReferenceFields.GovernmentIssuedDocumentDetails,
            PartyReferenceFields.DocumentDirectoryEntryInstanceReference,
            PartyReferenceFields.CorporateCustomerReference,
            PartyReferenceFields.CorporateCustomerLegalEntityReference,
            PartyReferenceFields.CompanyOfficerReference,
            PartyReferenceFields.CompanyOfficerRole,
        ];

        recorded.Distinct(StringComparer.Ordinal).Count().ShouldBe(24);
        PartyReferenceFields.Staged.ShouldBeEmpty();
    }

    [Fact]
    public void Given_legal_standing_and_documents_are_read_into_their_value_objects() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    PartyLegalStructure: "private limited company",
                    PoliticalExposureType: "politicalexposuredomestic",
                    PoliticalExposureDescription: " Member of a regional council since 2021 ",
                    CustomerSinceDate: "2015-03-02T10:00:00+01:00",
                    GovernmentIssuedDocument: new PartyDocumentInput("PersonalDocument", "23AB45678", "Passport", "2023-04-01", "2033-03-31"),
                    DocumentDirectoryEntryIdentification: " DOC-000123 "),
                [])
            .Value.ShouldBe(PartyReferenceChanges.None with
            {
                PartyLegalStructure = new(LegalStanding.LegalStructure),
                PoliticalExposureType = new(LegalStanding.PoliticalExposureType),
                PoliticalExposureDescription = new(LegalStanding.PoliticalExposureDescription),
                CustomerSinceDate = new(CustomerSince),
                GovernmentIssuedDocument = new(Documents.GovernmentIssuedDocument),
                DocumentDirectoryEntry = new(Documents.DocumentDirectoryEntry),
            });

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_legal_standing_or_document_clears_it(string blank) =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    PartyLegalStructure: blank,
                    PoliticalExposureType: blank,
                    PoliticalExposureDescription: blank,
                    CustomerSinceDate: blank,
                    GovernmentIssuedDocument: new PartyDocumentInput(blank, blank),
                    DocumentDirectoryEntryIdentification: blank),
                [])
            .Value.ShouldBe(PartyReferenceChanges.None with
            {
                PartyLegalStructure = new(null),
                PoliticalExposureType = new(null),
                PoliticalExposureDescription = new(null),
                CustomerSinceDate = new(null),
                GovernmentIssuedDocument = new(null),
                DocumentDirectoryEntry = new(null),
            });

    [Fact]
    public void Every_invalid_legal_standing_and_document_value_is_named_with_its_bian_path()
    {
        var error = PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    PartyLegalStructure: "Trust",
                    PoliticalExposureType: "PEP",
                    PoliticalExposureDescription: new string('d', 351),
                    CustomerSinceDate: "02/03/2015",
                    GovernmentIssuedDocument: new PartyDocumentInput("PersonalDocument", ValidFrom: "2033-01-01", ValidTo: "2023-01-01"),
                    DocumentDirectoryEntryIdentification: new string('D', 36)),
                [])
            .Error;

        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.Keys.Order(StringComparer.Ordinal).ShouldBe(
        [
            "CustomerSinceDate",
            "DocumentDirectoryEntryInstanceReference.DocumentDirectoryEntryIdentification",
            "GovernmentIssuedDocumentDetails.DocumentReference.DocumentIdentification.IdentifierValue",
            "GovernmentIssuedDocumentDetails.DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent",
            "PartyLegalStructure",
            "PoliticalExposureDescriptionRecord",
            "PoliticalExposureType",
        ]);
    }

    [Fact]
    public void A_customer_since_date_before_1900_is_invalid() =>
        PartyReferenceChanges.Create(new PartyReferenceInput(CustomerSinceDate: "1899-12-31"), []).Error
            .FieldErrors["CustomerSinceDate"].ShouldBe(["The customer-since date must not be before 1900-01-01."]);

    [Fact]
    public void Given_addresses_and_contact_points_are_read_into_their_value_objects() =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    ResidentialAddress: new PostalAddressInput(
                        StreetName: " Rue de Rivoli ",
                        StreetBuildingIdentification: "12",
                        PostCodeIdentification: "75001",
                        TownName: "Paris",
                        Country: new PartyCountryInput("fr")),
                    PartyAddress: new PostalAddressInput(
                        PostOfficeBox: "BP 42",
                        PostCodeIdentification: "1000",
                        TownName: "Bruxelles",
                        Country: new PartyCountryInput("BE")),
                    CorporateAddress: new PostalAddressInput(
                        "Finance",
                        null,
                        "Avenue Louise",
                        "54",
                        "Tour Louise",
                        "3",
                        null,
                        "1050",
                        "Ixelles",
                        new PartyCountryInput("BE")),
                    EmailAddress: " jeanne@example.fr ",
                    MobileNumber: "+33 6 12 34 56 78",
                    SocialNetworkUrl: "https://www.linkedin.com/in/jdupont"),
                [])
            .Value.ShouldBe(PartyReferenceChanges.None with
            {
                ResidentialAddress = new(Addresses.ResidentialAddress),
                PartyAddress = new(Addresses.PartyAddress),
                CorporateAddress = new(Addresses.CorporateAddress),
                EmailAddress = new(ContactPoints.EmailAddress),
                MobileNumber = new(ContactPoints.MobileNumber),
                SocialNetworkAddress = new(ContactPoints.SocialNetwork),
            });

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_address_or_contact_point_clears_it(string blank) =>
        PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    ResidentialAddress: new PostalAddressInput(),
                    PartyAddress: new PostalAddressInput(StreetName: blank, Country: new PartyCountryInput(blank)),
                    CorporateAddress: new PostalAddressInput(TownName: blank),
                    EmailAddress: blank,
                    MobileNumber: blank,
                    SocialNetworkUrl: blank),
                [])
            .Value.ShouldBe(PartyReferenceChanges.None with
            {
                ResidentialAddress = new(null),
                PartyAddress = new(null),
                CorporateAddress = new(null),
                EmailAddress = new(null),
                MobileNumber = new(null),
                SocialNetworkAddress = new(null),
            });

    [Fact]
    public void Every_invalid_address_part_and_contact_point_is_named_with_its_bian_path()
    {
        var error = PartyReferenceChanges.Create(
                new PartyReferenceInput(
                    ResidentialAddress: new PostalAddressInput(StreetName: "Rue de Rivoli", Country: new PartyCountryInput("FR")),
                    CorporateAddress: new PostalAddressInput(TownName: "Ixelles", Country: new PartyCountryInput("ZZ")),
                    EmailAddress: "jeanne.example.fr",
                    MobileNumber: "0612345678",
                    SocialNetworkUrl: "http://www.linkedin.com/in/jdupont"),
                [])
            .Error;

        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.ShouldNotBeNull().Keys.Order(StringComparer.Ordinal).ShouldBe(
        [
            "CellPhoneNumber.MobileNumber",
            "CorporateAddress.Country.Code",
            "ResidentialAddress.TownName",
            "SocialNetworkContacts.URLAddress",
            "eMailAddress.EmailAddress",
        ]);
    }
}
