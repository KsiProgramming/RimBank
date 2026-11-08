using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Fixed values for the entry's tests, so every run decides the same commands.
internal static class EntryExamples
{
    public static readonly DateTimeOffset OpenDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    public static readonly PartyReferenceDataDirectoryEntryId EntryId =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate));

    public static readonly DemographicsId Demographics = DemographicsId.From(Guid.CreateVersion7(OpenDate));

    public static readonly PartyReferenceId Reference = PartyReferenceId.From(Guid.CreateVersion7(OpenDate));

    public static readonly BankRelationsId BankRelations = BankRelationsId.From(Guid.CreateVersion7(OpenDate));

    public static RegisterPartyReferenceDataDirectoryEntry Register(PartyType partyType = PartyType.Person) =>
        new(EntryId, partyType, OpenDate, Demographics, Reference, BankRelations);

    public static readonly DateTimeOffset RefreshDate = OpenDate.AddDays(30);

    public static PartyRegistered Registered(PartyType partyType = PartyType.Person) =>
        new(EntryId, partyType, OpenDate, Demographics, Reference, BankRelations);

    public static UpdatePartyReferenceDataDirectoryEntryDetails Update(PartyType? partyType = PartyType.Organisation) =>
        new(partyType, RefreshDate);

    public static PartyReferenceDataDirectoryEntryUpdated Updated(PartyType partyType = PartyType.Organisation) =>
        new(EntryId, partyType, RefreshDate);

    public static readonly DateTimeOffset VerificationDate = OpenDate.AddDays(40);

    public static readonly PartyDemographics Details = new(
        "B2",
        new Employment("Contoso Ltd", "Accountant"),
        "Contoso Ltd since 2020",
        "MSc Finance",
        "Large print");

    public static DemographicsChanges ChangesTo(PartyDemographics details) =>
        new(details.SocioEconomicClassification, details.Employment, details.EmploymentHistory, details.EducationHistory, details.ServicingConstraints);

    public static UpdateDemographicsDetails UpdateDetails(DemographicsChanges? changes = null) =>
        new(Demographics, changes ?? ChangesTo(Details));

    public static DemographicsUpdated DetailsUpdated(
        PartyDemographics? details = null,
        DemographicsVerificationStatus status = DemographicsVerificationStatus.Unverified,
        DateTimeOffset? verificationDate = null) =>
        new(EntryId, Demographics, details ?? Details, status, verificationDate);

    public static ExchangeDemographicsDetails ExchangeDetails(ExchangeActionType action = ExchangeActionType.Verify) =>
        new(Demographics, action, VerificationDate);

    public static DemographicsExchanged DetailsExchanged(
        DemographicsVerificationStatus status = DemographicsVerificationStatus.Verified) =>
        new(EntryId, Demographics, status, VerificationDate);

    public const string Lei = "5493001KJTIIGC8Y1R12";

    public static readonly PartyIdentifiers Identifiers = new(
        TaxIdentificationNumber.TryCreate("FR12345678901").Value,
        NationalRegistrationNumber.TryCreate("1 85 05 78 006 084 36").Value,
        LegalEntityIdentifier.TryCreate(Lei).Value);

    public static readonly PartyReferenceDetails ReferenceDetails = new(Identifiers);

    public static PartyReferenceChanges ReferenceChangesTo(PartyIdentifiers identifiers) =>
        new(
            new PartyIdentifierChange<TaxIdentificationNumber>(identifiers.TaxIdentificationNumber),
            new PartyIdentifierChange<NationalRegistrationNumber>(identifiers.NationalRegistrationNumber),
            new PartyIdentifierChange<LegalEntityIdentifier>(identifiers.LegalEntityIdentifier));

    // The date the Reference handler stamps on its decision (the refresh date's UTC date).
    public static readonly DateOnly Today = DateOnly.FromDateTime(RefreshDate.UtcDateTime);

    public static readonly PartyNames Names = new(
        PartyFullLegalName.TryCreate("Jeanne Marie Dupont").Value,
        PartyNameSalutation.TryCreate("Ms").Value);

    public static readonly DateOnly DateOfBirth = new(1990, 5, 17);

    public static readonly PartyResidency Residency = new(
        CountryCode.From("FR"),
        CountryCode.From("BE"),
        new ResidencyStatus(ResidencyStatusCode.NonResident, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    // Every value roadmap 1.10 and 1.18 record.
    public static readonly PartyReferenceDetails FullReferenceDetails = new(Identifiers, Names, DateOfBirth, Residency);

    public static readonly PartyAddresses Addresses = new(
        new PostalAddress(null, null, "Rue de Rivoli", "12", null, null, null, "75001", "Paris", CountryCode.From("FR")),
        new PostalAddress(null, null, null, null, null, null, "BP 42", "1000", "Bruxelles", CountryCode.From("BE")),
        new PostalAddress("Finance", null, "Avenue Louise", "54", "Tour Louise", "3", null, "1050", "Ixelles", CountryCode.From("BE")));

    public static readonly PartyContactPoints ContactPoints = new(
        EmailAddress.TryCreate("jeanne@example.fr").Value,
        MobileNumber.TryCreate("+33612345678").Value,
        SocialNetworkAddress.TryCreate("https://www.linkedin.com/in/jdupont").Value);

    // Every value roadmap 1.10, 1.18 and 1.19 record.
    public static readonly PartyReferenceDetails CompleteReferenceDetails =
        FullReferenceDetails with { Addresses = Addresses, ContactPoints = ContactPoints };

    public static readonly DateOnly CustomerSince = new(2015, 3, 2);

    public static readonly PartyLegalStanding LegalStanding = new(
        PartyLegalStructure.PrivateLimitedCompany,
        PoliticalExposureType.Domestic,
        PoliticalExposureDescription.TryCreate("Member of a regional council since 2021").Value,
        CustomerSince);

    public static readonly PartyDocuments Documents = new(
        new GovernmentIssuedDocument(
            PartyDocumentType.PersonalDocument,
            GovernmentIssuedDocumentNumber.TryCreate("23AB45678").Value,
            GovernmentIssuedDocumentName.TryCreate("Passport").Value,
            new DateOnly(2023, 4, 1),
            new DateOnly(2033, 3, 31)),
        DocumentDirectoryEntryReference.TryCreate("DOC-000123").Value);

    // Every value roadmap 1.10, 1.18, 1.19 and 1.20 record.
    public static readonly PartyReferenceDetails FullestReferenceDetails =
        CompleteReferenceDetails with { LegalStanding = LegalStanding, Documents = Documents };

    // Two other entries a Reference can name (1.21): an organisation (the company and its legal entity) and a person (the
    // company officer), registered at the same time as the entry.
    public static readonly PartyReferenceDataDirectoryEntryId CompanyId =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate.AddMinutes(1)));

    public static readonly PartyReferenceDataDirectoryEntryId OfficerId =
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate.AddMinutes(2)));

    public static PartyRegistered RegisteredEntry(PartyReferenceDataDirectoryEntryId entryId, PartyType partyType) =>
        new(entryId, partyType, OpenDate, Demographics, Reference, BankRelations);

    public static readonly PartyCorporateReferences CorporateReferences = new(
        CompanyId,
        CompanyId,
        OfficerId,
        CompanyOfficerRole.TryCreate("Director").Value);

    // The changes that set every 1.21 reference to the given ones.
    public static PartyReferenceChanges CorporateReferenceChangesTo(PartyCorporateReferences references) =>
        PartyReferenceChanges.None with
        {
            CorporateCustomer = new(references.CorporateCustomer),
            LegalEntity = new(references.LegalEntity),
            CompanyOfficer = new(references.CompanyOfficer),
            CompanyOfficerRole = new(references.CompanyOfficerRole),
        };

    // The party types of the company and the officer, as the Reference handler loads them.
    public static readonly IReadOnlyDictionary<PartyReferenceDataDirectoryEntryId, PartyType> ReferencedParties =
        new Dictionary<PartyReferenceDataDirectoryEntryId, PartyType>
        {
            [CompanyId] = PartyType.Organisation,
            [OfficerId] = PartyType.Person,
        };

    // The changes that set every value of the details. The 1.19 and 1.20 parts are only sent when the details have them,
    // so the changes of an earlier-shaped Reference stay the ones its own phase's request makes.
    public static PartyReferenceChanges ReferenceChangesTo(PartyReferenceDetails details) =>
        ReferenceChangesTo(details.Identifiers) with
        {
            PartyFullLegalName = new(details.CurrentNames.FullLegalName),
            PartyNameSalutation = new(details.CurrentNames.Salutation),
            DateOfBirth = new(details.DateOfBirth),
            Nationality = new(details.CurrentResidency.Nationality),
            PartyCountryOfResidence = new(details.CurrentResidency.CountryOfResidence),
            ResidencyStatus = new(details.CurrentResidency.ResidencyStatus),
            ResidentialAddress = details.Addresses is { } residential ? new(residential.ResidentialAddress) : null,
            PartyAddress = details.Addresses is { } party ? new(party.PartyAddress) : null,
            CorporateAddress = details.Addresses is { } corporate ? new(corporate.CorporateAddress) : null,
            EmailAddress = details.ContactPoints is { } email ? new(email.EmailAddress) : null,
            MobileNumber = details.ContactPoints is { } mobile ? new(mobile.MobileNumber) : null,
            SocialNetworkAddress = details.ContactPoints is { } social ? new(social.SocialNetwork) : null,
            PartyLegalStructure = details.LegalStanding is { } structure ? new(structure.LegalStructure) : null,
            PoliticalExposureType = details.LegalStanding is { } exposure ? new(exposure.PoliticalExposureType) : null,
            PoliticalExposureDescription = details.LegalStanding is { } description ? new(description.PoliticalExposureDescription) : null,
            CustomerSinceDate = details.LegalStanding is { } since ? new(since.CustomerSince) : null,
            GovernmentIssuedDocument = details.Documents is { } document ? new(document.GovernmentIssuedDocument) : null,
            DocumentDirectoryEntry = details.Documents is { } directory ? new(directory.DocumentDirectoryEntry) : null,
            CorporateCustomer = details.CorporateReferences is { } customer ? new(customer.CorporateCustomer) : null,
            LegalEntity = details.CorporateReferences is { } legal ? new(legal.LegalEntity) : null,
            CompanyOfficer = details.CorporateReferences is { } officer ? new(officer.CompanyOfficer) : null,
            CompanyOfficerRole = details.CorporateReferences is { } role ? new(role.CompanyOfficerRole) : null,
        };

    public static UpdatePartyReferenceDetails UpdateReference(
        PartyReferenceChanges? changes = null,
        IReadOnlyDictionary<PartyReferenceDataDirectoryEntryId, PartyType>? referencedParties = null) =>
        new(Reference, changes ?? ReferenceChangesTo(Identifiers), Today, referencedParties);

    public static PartyReferenceUpdated ReferenceUpdated(PartyReferenceDetails? details = null) =>
        new(EntryId, Reference, details ?? ReferenceDetails);
}
