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

    public static PartyReferenceChanges ReferenceChangesTo(PartyReferenceDetails details) =>
        ReferenceChangesTo(details.Identifiers) with
        {
            PartyFullLegalName = new(details.CurrentNames.FullLegalName),
            PartyNameSalutation = new(details.CurrentNames.Salutation),
            DateOfBirth = new(details.DateOfBirth),
            Nationality = new(details.CurrentResidency.Nationality),
            PartyCountryOfResidence = new(details.CurrentResidency.CountryOfResidence),
            ResidencyStatus = new(details.CurrentResidency.ResidencyStatus),
        };

    public static UpdatePartyReferenceDetails UpdateReference(PartyReferenceChanges? changes = null) =>
        new(Reference, changes ?? ReferenceChangesTo(Identifiers), Today);

    public static PartyReferenceUpdated ReferenceUpdated(PartyReferenceDetails? details = null) =>
        new(EntryId, Reference, details ?? ReferenceDetails);
}
