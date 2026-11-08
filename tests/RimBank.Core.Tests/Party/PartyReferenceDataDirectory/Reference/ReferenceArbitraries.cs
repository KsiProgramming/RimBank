using FsCheck;
using FsCheck.Fluent;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Valid Reference values for every recorded field, and the changes that set, clear or keep them.
internal static class ReferenceArbitraries
{
    private static Gen<T?> OrNull<T>(params T[] values)
        where T : class =>
        Gen.Elements([null, .. values]);

    private static Gen<T?> OrNullValue<T>(params T[] values)
        where T : struct =>
        Gen.Elements([null, .. values.Select(value => (T?)value)]);

    private static readonly Gen<PartyIdentifiers> _identifiers =
        from tax in OrNull(TaxIdentificationNumber.TryCreate("FR12345678901").Value, TaxIdentificationNumber.TryCreate("BE0123456789").Value)
        from national in OrNull(NationalRegistrationNumber.TryCreate("185057800608436").Value)
        from lei in OrNull(LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value)
        select new PartyIdentifiers(tax, national, lei);

    private static readonly Gen<PostalAddress?> _addresses = OrNull(
        new PostalAddress(null, null, "Rue de Rivoli", "12", null, null, null, "75001", "Paris", CountryCode.From("FR")),
        new PostalAddress("Finance", null, "Avenue Louise", "54", null, "3", null, "1050", "Ixelles", CountryCode.From("BE")));

    private static readonly Gen<GovernmentIssuedDocument?> _documents = OrNull(
        new GovernmentIssuedDocument(
            PartyDocumentType.PersonalDocument,
            GovernmentIssuedDocumentNumber.TryCreate("23AB45678").Value,
            GovernmentIssuedDocumentName.TryCreate("Passport").Value,
            new DateOnly(2023, 4, 1),
            new DateOnly(2033, 3, 31)),
        new GovernmentIssuedDocument(
            PartyDocumentType.WorkDocument,
            GovernmentIssuedDocumentNumber.TryCreate("WP-1").Value,
            null,
            null,
            null));

    // Built by applying changes that set every field to the empty Reference, so each generated Reference has the shape
    // Apply gives (an empty group is null).
    public static readonly Gen<PartyReferenceDetails> Details =
        from identifiers in _identifiers
        from fullName in OrNull(PartyFullLegalName.TryCreate("Jeanne Marie Dupont").Value)
        from salutation in OrNull(PartyNameSalutation.TryCreate("Ms").Value)
        from birth in OrNullValue(new DateOnly(1990, 5, 17), new DateOnly(1950, 1, 1))
        from nationality in OrNullValue(CountryCode.From("FR"), CountryCode.From("BE"))
        from residence in OrNullValue(CountryCode.From("BE"))
        from status in OrNull(new ResidencyStatus(ResidencyStatusCode.Resident, null))
        from residential in _addresses
        from party in _addresses
        from corporate in _addresses
        from email in OrNull(EmailAddress.TryCreate("jeanne@example.fr").Value)
        from mobile in OrNull(MobileNumber.TryCreate("+33612345678").Value)
        from social in OrNull(SocialNetworkAddress.TryCreate("https://www.linkedin.com/in/jdupont").Value)
        from structure in OrNullValue(PartyLegalStructure.CoOperative, PartyLegalStructure.Individual)
        from exposure in OrNullValue(PoliticalExposureType.Domestic, PoliticalExposureType.NoExposure)
        from description in OrNull(PoliticalExposureDescription.TryCreate("Mayor").Value)
        from since in OrNullValue(new DateOnly(2015, 3, 2))
        from document in _documents
        from directory in OrNull(DocumentDirectoryEntryReference.TryCreate("DOC-000123").Value)
        select PartyReferenceDetails.Empty.Apply(new PartyReferenceChanges(
            new(identifiers.TaxIdentificationNumber),
            new(identifiers.NationalRegistrationNumber),
            new(identifiers.LegalEntityIdentifier),
            new(fullName),
            new(salutation),
            new(birth),
            new(nationality),
            new(residence),
            new(status),
            new(residential),
            new(party),
            new(corporate),
            new(email),
            new(mobile),
            new(social),
            new(structure),
            new(exposure),
            new(description),
            new(since),
            new(document),
            new(directory)));

    // For each field: omitted (keep), cleared, or set to a generated value.
    public static readonly Gen<PartyReferenceChanges> PartialChanges =
        from target in Details
        from sent in Gen.ArrayOf(Gen.Elements(0, 1, 2), 21)
        select Pick(ChangesTo(target), sent);

    // The changes that set every field of the details, clearing the unknown ones.
    public static PartyReferenceChanges ChangesTo(PartyReferenceDetails details) =>
        new(
            new(details.Identifiers.TaxIdentificationNumber),
            new(details.Identifiers.NationalRegistrationNumber),
            new(details.Identifiers.LegalEntityIdentifier),
            new(details.CurrentNames.FullLegalName),
            new(details.CurrentNames.Salutation),
            new(details.DateOfBirth),
            new(details.CurrentResidency.Nationality),
            new(details.CurrentResidency.CountryOfResidence),
            new(details.CurrentResidency.ResidencyStatus),
            new(details.CurrentAddresses.ResidentialAddress),
            new(details.CurrentAddresses.PartyAddress),
            new(details.CurrentAddresses.CorporateAddress),
            new(details.CurrentContactPoints.EmailAddress),
            new(details.CurrentContactPoints.MobileNumber),
            new(details.CurrentContactPoints.SocialNetwork),
            new(details.CurrentLegalStanding.LegalStructure),
            new(details.CurrentLegalStanding.PoliticalExposureType),
            new(details.CurrentLegalStanding.PoliticalExposureDescription),
            new(details.CurrentLegalStanding.CustomerSince),
            new(details.CurrentDocuments.GovernmentIssuedDocument),
            new(details.CurrentDocuments.DocumentDirectoryEntry));

    // 0 omits the field, 1 clears it, 2 sends the full change's value.
    private static PartyReferenceChanges Pick(PartyReferenceChanges full, int[] sent) =>
        new(
            sent[0] switch { 0 => null, 1 => new(null), _ => full.TaxIdentificationNumber },
            sent[1] switch { 0 => null, 1 => new(null), _ => full.NationalRegistrationNumber },
            sent[2] switch { 0 => null, 1 => new(null), _ => full.LegalEntityIdentifier },
            Choose(full.PartyFullLegalName, sent[3]),
            Choose(full.PartyNameSalutation, sent[4]),
            ChooseValue(full.DateOfBirth, sent[5]),
            ChooseValue(full.Nationality, sent[6]),
            ChooseValue(full.PartyCountryOfResidence, sent[7]),
            Choose(full.ResidencyStatus, sent[8]),
            Choose(full.ResidentialAddress, sent[9]),
            Choose(full.PartyAddress, sent[10]),
            Choose(full.CorporateAddress, sent[11]),
            Choose(full.EmailAddress, sent[12]),
            Choose(full.MobileNumber, sent[13]),
            Choose(full.SocialNetworkAddress, sent[14]),
            ChooseValue(full.PartyLegalStructure, sent[15]),
            ChooseValue(full.PoliticalExposureType, sent[16]),
            Choose(full.PoliticalExposureDescription, sent[17]),
            ChooseValue(full.CustomerSinceDate, sent[18]),
            Choose(full.GovernmentIssuedDocument, sent[19]),
            Choose(full.DocumentDirectoryEntry, sent[20]));

    private static PartyReferenceChange<T?>? Choose<T>(PartyReferenceChange<T?>? full, int sent)
        where T : class =>
        sent switch
        {
            0 => null,
            1 => new(null),
            _ => full,
        };

    private static PartyReferenceChange<T?>? ChooseValue<T>(PartyReferenceChange<T?>? full, int sent)
        where T : struct =>
        sent switch
        {
            0 => null,
            1 => new(null),
            _ => full,
        };
}
