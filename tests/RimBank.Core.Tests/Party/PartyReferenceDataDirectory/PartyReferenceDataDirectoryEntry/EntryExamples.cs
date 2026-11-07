
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
}
