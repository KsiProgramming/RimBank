using System.Globalization;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of the Reference tools: the fields of the HTTP Reference response with BIAN's names (ADR 0029), which it
/// mirrors (a parity test proves the JSON is equal, ADR 0039), and the entry's <see cref="Version"/>, which HTTP sends as
/// the <c>ETag</c> header. Update builds it from its acknowledgement, Retrieve from the entry's view (tech-stack §5).
/// Unknown values, and objects with none, are left out.
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
/// <param name="Version">The entry's version: the <c>expectedVersion</c> of a later command (ADR 0027).</param>
public sealed record PartyReferenceResult(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("GovernmentIssuedIdentityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GovernmentIssuedIdentityModel? GovernmentIssuedIdentityReference,
    [property: JsonPropertyName("PartyLegalEntityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PartyLegalEntityModel? PartyLegalEntityReference,
    [property: JsonPropertyName("PartyFullLegalName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyFullLegalName,
    [property: JsonPropertyName("PartyNameSalutation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyNameSalutation,
    [property: JsonPropertyName("DateofBirth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DateofBirth,
    [property: JsonPropertyName("Nationality"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CountryModel? Nationality,
    [property: JsonPropertyName("PartyCountryOfResidence"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CountryModel? PartyCountryOfResidence,
    [property: JsonPropertyName("ResidencyStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ResidencyStatusModel? ResidencyStatus,
    [property: JsonPropertyName("ResidentialAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PostalAddressModel? ResidentialAddress,
    [property: JsonPropertyName("PartyAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PostalAddressModel? PartyAddress,
    [property: JsonPropertyName("CorporateAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PostalAddressModel? CorporateAddress,
    [property: JsonPropertyName("eMailAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EmailAddressModel? EMailAddress,
    [property: JsonPropertyName("CellPhoneNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MobileNumberModel? CellPhoneNumber,
    [property: JsonPropertyName("SocialNetworkContacts"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SocialNetworkContactModel? SocialNetworkContacts,
    [property: JsonPropertyName("PartyLegalStructure"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyLegalStructure,
    [property: JsonPropertyName("PoliticalExposureType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PoliticalExposureType,
    [property: JsonPropertyName("PoliticalExposureDescriptionRecord"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PoliticalExposureDescriptionRecord,
    [property: JsonPropertyName("CustomerSinceDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CustomerSinceDate,
    [property: JsonPropertyName("GovernmentIssuedDocumentDetails"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PartyDocumentModel? GovernmentIssuedDocumentDetails,
    [property: JsonPropertyName("DocumentDirectoryEntryInstanceReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DocumentDirectoryEntryModel? DocumentDirectoryEntryInstanceReference,
    [property: JsonPropertyName("CorporateCustomerReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InvolvedPartyModel? CorporateCustomerReference,
    [property: JsonPropertyName("CorporateCustomerLegalEntityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InvolvedPartyModel? CorporateCustomerLegalEntityReference,
    [property: JsonPropertyName("CompanyOfficerReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InvolvedPartyModel? CompanyOfficerReference,
    [property: JsonPropertyName("CompanyOfficerRole"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CompanyOfficerRole,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The result for an Update's acknowledgement.</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdatePartyReference"/>.</param>
    /// <returns>The tool's result.</returns>
    public static PartyReferenceResult From(UpdatePartyReferenceAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.EntryId, acknowledged.PartyReferenceId, acknowledged.Reference, acknowledged.Version);
    }

    /// <summary>The result for the Reference section of an entry's view.</summary>
    /// <param name="view">The entry's view, as <see cref="RetrievePartyReference"/> returns it.</param>
    /// <returns>The tool's result.</returns>
    public static PartyReferenceResult From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.EntryId, view.PartyReferenceId, view.CurrentReference, view.Version);
    }

    private static PartyReferenceResult From(
        PartyReferenceDataDirectoryEntryId entryId,
        PartyReferenceId referenceId,
        PartyReferenceDetails reference,
        long version)
    {
        var identifiers = reference.Identifiers;
        var names = reference.CurrentNames;
        var residency = reference.CurrentResidency;
        var addresses = reference.CurrentAddresses;
        var contactPoints = reference.CurrentContactPoints;
        var legalStanding = reference.CurrentLegalStanding;
        var documents = reference.CurrentDocuments;
        var corporate = reference.CurrentCorporateReferences;
        return new PartyReferenceResult(
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
            corporate.CompanyOfficerRole?.Value,
            version);
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
