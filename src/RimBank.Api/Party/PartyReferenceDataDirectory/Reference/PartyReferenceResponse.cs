using System.Globalization;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of every successful Reference operation: the BIAN Reference fields RimBank records (ADR 0029) and the ids of
/// the entry and its Reference. Update builds it from its acknowledgement, Retrieve from the entry's view (tech-stack §5).
/// Unknown values, and objects with none, are left out, and the entry's version is sent as the <c>ETag</c> header, not in
/// the body.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> of its operations.</param>
/// <param name="ReferenceId">The entry's Reference: the <c>{referenceid}</c> of its operations.</param>
/// <param name="GovernmentIssuedIdentityReference">BIAN <c>GovernmentIssuedIdentityReference</c>.</param>
/// <param name="PartyLegalEntityReference">BIAN <c>PartyLegalEntityReference</c>, with the LEI.</param>
/// <param name="PartyFullLegalName">BIAN <c>PartyFullLegalName</c>.</param>
/// <param name="PartyNameSalutation">BIAN <c>PartyNameSalutation</c>.</param>
/// <param name="DateofBirth">BIAN <c>DateofBirth</c>, as an ISO 8601 date (<c>YYYY-MM-DD</c>, ADR 0029).</param>
/// <param name="Nationality">BIAN <c>Nationality</c>.</param>
/// <param name="PartyCountryOfResidence">BIAN <c>PartyCountryOfResidence</c>.</param>
/// <param name="ResidencyStatus">BIAN <c>ResidencyStatus</c>.</param>
/// <param name="ResidentialAddress">BIAN <c>ResidentialAddress</c>.</param>
/// <param name="PartyAddress">BIAN <c>PartyAddress</c>.</param>
/// <param name="CorporateAddress">BIAN <c>CorporateAddress</c>.</param>
/// <param name="EMailAddress">BIAN <c>eMailAddress</c>.</param>
/// <param name="CellPhoneNumber">BIAN <c>CellPhoneNumber</c>.</param>
/// <param name="SocialNetworkContacts">BIAN <c>SocialNetworkContacts</c>.</param>
/// <param name="PartyLegalStructure">BIAN <c>PartyLegalStructure</c>, in BIAN spelling (for example <c>Co-Operative</c>).</param>
/// <param name="PoliticalExposureType">BIAN <c>PoliticalExposureType</c>, in BIAN spelling.</param>
/// <param name="PoliticalExposureDescriptionRecord">BIAN <c>PoliticalExposureDescriptionRecord</c>.</param>
/// <param name="CustomerSinceDate">BIAN <c>CustomerSinceDate</c>, as an ISO 8601 date (<c>YYYY-MM-DD</c>, ADR 0032).</param>
/// <param name="GovernmentIssuedDocumentDetails">BIAN <c>GovernmentIssuedDocumentDetails</c>.</param>
/// <param name="DocumentDirectoryEntryInstanceReference">BIAN <c>DocumentDirectoryEntryInstanceReference</c>.</param>
/// <param name="CorporateCustomerReference">BIAN <c>CorporateCustomerReference</c>, with the entry id in its <c>PartyReference</c>.</param>
/// <param name="CorporateCustomerLegalEntityReference">BIAN <c>CorporateCustomerLegalEntityReference</c>.</param>
/// <param name="CompanyOfficerReference">BIAN <c>CompanyOfficerReference</c>.</param>
/// <param name="CompanyOfficerRole">BIAN <c>CompanyOfficerRole</c>, as the role text (ADR 0034).</param>
public sealed record PartyReferenceResponse(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("GovernmentIssuedIdentityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GovernmentIssuedIdentityModel? GovernmentIssuedIdentityReference,
    [property: JsonPropertyName("PartyLegalEntityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PartyLegalEntityModel? PartyLegalEntityReference,
    [property: JsonPropertyName("PartyFullLegalName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyFullLegalName = null,
    [property: JsonPropertyName("PartyNameSalutation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyNameSalutation = null,
    [property: JsonPropertyName("DateofBirth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DateofBirth = null,
    [property: JsonPropertyName("Nationality"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CountryModel? Nationality = null,
    [property: JsonPropertyName("PartyCountryOfResidence"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CountryModel? PartyCountryOfResidence = null,
    [property: JsonPropertyName("ResidencyStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ResidencyStatusModel? ResidencyStatus = null,
    [property: JsonPropertyName("ResidentialAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PostalAddressModel? ResidentialAddress = null,
    [property: JsonPropertyName("PartyAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PostalAddressModel? PartyAddress = null,
    [property: JsonPropertyName("CorporateAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PostalAddressModel? CorporateAddress = null,
    [property: JsonPropertyName("eMailAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EmailAddressModel? EMailAddress = null,
    [property: JsonPropertyName("CellPhoneNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MobileNumberModel? CellPhoneNumber = null,
    [property: JsonPropertyName("SocialNetworkContacts"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SocialNetworkContactModel? SocialNetworkContacts = null,
    [property: JsonPropertyName("PartyLegalStructure"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyLegalStructure = null,
    [property: JsonPropertyName("PoliticalExposureType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PoliticalExposureType = null,
    [property: JsonPropertyName("PoliticalExposureDescriptionRecord"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PoliticalExposureDescriptionRecord = null,
    [property: JsonPropertyName("CustomerSinceDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CustomerSinceDate = null,
    [property: JsonPropertyName("GovernmentIssuedDocumentDetails"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PartyDocumentModel? GovernmentIssuedDocumentDetails = null,
    [property: JsonPropertyName("DocumentDirectoryEntryInstanceReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DocumentDirectoryEntryModel? DocumentDirectoryEntryInstanceReference = null,
    [property: JsonPropertyName("CorporateCustomerReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InvolvedPartyModel? CorporateCustomerReference = null,
    [property: JsonPropertyName("CorporateCustomerLegalEntityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InvolvedPartyModel? CorporateCustomerLegalEntityReference = null,
    [property: JsonPropertyName("CompanyOfficerReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InvolvedPartyModel? CompanyOfficerReference = null,
    [property: JsonPropertyName("CompanyOfficerRole"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CompanyOfficerRole = null)
{
    /// <summary>The response for an Update's acknowledgement.</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdatePartyReference"/>.</param>
    /// <returns>The response body.</returns>
    public static PartyReferenceResponse From(UpdatePartyReferenceAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.EntryId, acknowledged.PartyReferenceId, acknowledged.Reference);
    }

    /// <summary>The response for the Reference section of an entry's view.</summary>
    /// <param name="view">The entry's view, as <see cref="RetrievePartyReference"/> returns it.</param>
    /// <returns>The response body.</returns>
    public static PartyReferenceResponse From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.EntryId, view.PartyReferenceId, view.CurrentReference);
    }

    private static PartyReferenceResponse From(
        PartyReferenceDataDirectoryEntryId entryId,
        PartyReferenceId referenceId,
        PartyReferenceDetails reference)
    {
        var identifiers = reference.Identifiers;
        var names = reference.CurrentNames;
        var residency = reference.CurrentResidency;
        var addresses = reference.CurrentAddresses;
        var contactPoints = reference.CurrentContactPoints;
        var legalStanding = reference.CurrentLegalStanding;
        var documents = reference.CurrentDocuments;
        var corporate = reference.CurrentCorporateReferences;
        return new PartyReferenceResponse(
            entryId.Value,
            referenceId.Value,
            (identifiers.TaxIdentificationNumber, identifiers.NationalRegistrationNumber) switch
            {
                (null, null) => null,
                var (tax, national) => new GovernmentIssuedIdentityModel
                {
                    TaxIdentificationNumber = tax?.Value,
                    NationalRegistrationNumber = national?.Value,
                },
            },
            identifiers.LegalEntityIdentifier switch
            {
                null => null,
                var lei => new PartyLegalEntityModel { Identification = lei.Value },
            },
            names.FullLegalName?.Value,
            names.Salutation?.Value,
            reference.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CountryOf(residency.Nationality),
            CountryOf(residency.CountryOfResidence),
            residency.ResidencyStatus switch
            {
                null => null,
                var status => new ResidencyStatusModel
                {
                    StatusDescription = status.Code.ToString(),
                    StatusDateTime = status.StatusDateTime?.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
                },
            },
            PostalAddressModel.From(addresses.ResidentialAddress),
            PostalAddressModel.From(addresses.PartyAddress),
            PostalAddressModel.From(addresses.CorporateAddress),
            contactPoints.EmailAddress is { } email ? new EmailAddressModel { EmailAddress = email.Value } : null,
            contactPoints.MobileNumber is { } mobile ? new MobileNumberModel { MobileNumber = mobile.Value } : null,
            contactPoints.SocialNetwork is { } social ? new SocialNetworkContactModel { URLAddress = social.Value } : null,
            legalStanding.LegalStructure?.ToBianName(),
            legalStanding.PoliticalExposureType?.ToBianName(),
            legalStanding.PoliticalExposureDescription?.Value,
            legalStanding.CustomerSince?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PartyDocumentModel.From(documents.GovernmentIssuedDocument),
            documents.DocumentDirectoryEntry is { } directory
                ? new DocumentDirectoryEntryModel { DocumentDirectoryEntryIdentification = directory.Value }
                : null,
            InvolvedPartyOf(corporate.CorporateCustomer),
            InvolvedPartyOf(corporate.LegalEntity),
            InvolvedPartyOf(corporate.CompanyOfficer),
            corporate.CompanyOfficerRole?.Value);
    }

    private static InvolvedPartyModel? InvolvedPartyOf(PartyReferenceDataDirectoryEntryId? entryId) =>
        entryId switch
        {
            null => null,
            { } referenced => new InvolvedPartyModel { PartyReference = referenced.ToString() },
        };

    private static CountryModel? CountryOf(SharedKernel.CountryCode? country) =>
        country switch
        {
            null => null,
            { } code => new CountryModel { Code = code.Code },
        };
}
