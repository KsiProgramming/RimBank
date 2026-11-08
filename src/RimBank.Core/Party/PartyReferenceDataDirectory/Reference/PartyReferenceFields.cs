using System.Collections.Frozen;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The BIAN names of the Reference fields and parameters, used as the keys of field errors so HTTP and MCP name them
/// the same way, and the roadmap phase that will record each BIAN Reference field RimBank does not record yet (ADR 0029).
/// </summary>
public static class PartyReferenceFields
{
    /// <summary>The BIAN parameter that names the entry's Reference.</summary>
    public const string ReferenceId = "referenceid";

    /// <summary>BIAN <c>GovernmentIssuedIdentityReference</c>, which holds the government-issued identifiers.</summary>
    public const string GovernmentIssuedIdentityReference = "GovernmentIssuedIdentityReference";

    /// <summary>BIAN <c>PartyLegalEntityReference</c>, which holds the LEI.</summary>
    public const string PartyLegalEntityReference = "PartyLegalEntityReference";

    /// <summary>BIAN <c>GovernmentIssuedIdentityReference.TaxIdentificationNumber</c>.</summary>
    public const string TaxIdentificationNumber = GovernmentIssuedIdentityReference + ".TaxIdentificationNumber";

    /// <summary>BIAN <c>GovernmentIssuedIdentityReference.NationalRegistrationNumber</c>.</summary>
    public const string NationalRegistrationNumber = GovernmentIssuedIdentityReference + ".NationalRegistrationNumber";

    /// <summary>BIAN <c>PartyLegalEntityReference.Identification</c>: the LEI (ADR 0029).</summary>
    public const string LegalEntityIdentifier = PartyLegalEntityReference + ".Identification";

    /// <summary>BIAN <c>PartyFullLegalName</c> (1.18).</summary>
    public const string PartyFullLegalName = "PartyFullLegalName";

    /// <summary>BIAN <c>PartyNameSalutation</c> (1.18).</summary>
    public const string PartyNameSalutation = "PartyNameSalutation";

    /// <summary>BIAN <c>DateofBirth</c> (1.18).</summary>
    public const string DateofBirth = "DateofBirth";

    /// <summary>BIAN <c>Nationality</c> (1.18), a <c>Country</c> object.</summary>
    public const string Nationality = "Nationality";

    /// <summary>BIAN <c>Nationality.Code</c>: the ISO 3166 code of the nationality.</summary>
    public const string NationalityCode = Nationality + "." + CountryCodePart;

    /// <summary>BIAN <c>PartyCountryOfResidence</c> (1.18), a <c>Country</c> object.</summary>
    public const string PartyCountryOfResidence = "PartyCountryOfResidence";

    /// <summary>BIAN <c>PartyCountryOfResidence.Code</c>: the ISO 3166 code of the country of residence.</summary>
    public const string PartyCountryOfResidenceCode = PartyCountryOfResidence + "." + CountryCodePart;

    /// <summary>BIAN <c>ResidencyStatus</c> (1.18), an ISO 20022 <c>Status</c> object.</summary>
    public const string ResidencyStatus = "ResidencyStatus";

    /// <summary>BIAN <c>ResidencyStatus.StatusDescription</c>: <c>Resident</c> or <c>NonResident</c>.</summary>
    public const string ResidencyStatusDescription = ResidencyStatus + "." + StatusDescriptionPart;

    /// <summary>BIAN <c>ResidencyStatus.StatusDateTime</c>: when the status was assigned.</summary>
    public const string ResidencyStatusDateTime = ResidencyStatus + "." + StatusDateTimePart;

    /// <summary>BIAN <c>ResidentialAddress</c> (1.19), a <c>PostalAddress</c> object.</summary>
    public const string ResidentialAddress = "ResidentialAddress";

    /// <summary>BIAN <c>PartyAddress</c> (1.19), a <c>PostalAddress</c> object.</summary>
    public const string PartyAddress = "PartyAddress";

    /// <summary>BIAN <c>CorporateAddress</c> (1.19), a <c>PostalAddress</c> object.</summary>
    public const string CorporateAddress = "CorporateAddress";

    /// <summary>BIAN <c>eMailAddress</c> (1.19), a <c>ContactPoint</c> object.</summary>
    public const string EMailAddress = "eMailAddress";

    /// <summary>The ISO 20022 <c>ElectronicAddress</c> part that holds an email address (ADR 0031).</summary>
    public const string EmailAddressPart = "EmailAddress";

    /// <summary>BIAN <c>eMailAddress.EmailAddress</c>.</summary>
    public const string EMailAddressEmailAddress = EMailAddress + "." + EmailAddressPart;

    /// <summary>BIAN <c>CellPhoneNumber</c> (1.19), a <c>ContactPoint</c> object.</summary>
    public const string CellPhoneNumber = "CellPhoneNumber";

    /// <summary>The ISO 20022 <c>PhoneAddress</c> part that holds a mobile number (ADR 0031).</summary>
    public const string MobileNumberPart = "MobileNumber";

    /// <summary>BIAN <c>CellPhoneNumber.MobileNumber</c>.</summary>
    public const string CellPhoneNumberMobileNumber = CellPhoneNumber + "." + MobileNumberPart;

    /// <summary>BIAN <c>SocialNetworkContacts</c> (1.19), a <c>ContactPoint</c> object.</summary>
    public const string SocialNetworkContacts = "SocialNetworkContacts";

    /// <summary>The ISO 20022 <c>ElectronicAddress</c> part that holds a URL (ADR 0031).</summary>
    public const string UrlAddressPart = "URLAddress";

    /// <summary>BIAN <c>SocialNetworkContacts.URLAddress</c>.</summary>
    public const string SocialNetworkContactsUrlAddress = SocialNetworkContacts + "." + UrlAddressPart;

    /// <summary>BIAN <c>PartyLegalStructure</c> (1.20).</summary>
    public const string PartyLegalStructure = "PartyLegalStructure";

    /// <summary>BIAN <c>PoliticalExposureType</c> (1.20).</summary>
    public const string PoliticalExposureType = "PoliticalExposureType";

    /// <summary>BIAN <c>PoliticalExposureDescriptionRecord</c> (1.20).</summary>
    public const string PoliticalExposureDescriptionRecord = "PoliticalExposureDescriptionRecord";

    /// <summary>BIAN <c>CustomerSinceDate</c> (1.20).</summary>
    public const string CustomerSinceDate = "CustomerSinceDate";

    /// <summary>BIAN <c>GovernmentIssuedDocumentDetails</c> (1.20), a <c>PartyDocument</c> object.</summary>
    public const string GovernmentIssuedDocumentDetails = "GovernmentIssuedDocumentDetails";

    /// <summary>BIAN <c>DocumentDirectoryEntryInstanceReference</c> (1.20), a <c>DocumentDirectoryEntry</c> object.</summary>
    public const string DocumentDirectoryEntryInstanceReference = "DocumentDirectoryEntryInstanceReference";

    /// <summary>The part of a BIAN <c>DocumentDirectoryEntry</c> object RimBank records.</summary>
    public const string DocumentDirectoryEntryIdentificationPart = "DocumentDirectoryEntryIdentification";

    /// <summary>BIAN <c>DocumentDirectoryEntryInstanceReference.DocumentDirectoryEntryIdentification</c>.</summary>
    public const string DocumentDirectoryEntryIdentification =
        DocumentDirectoryEntryInstanceReference + "." + DocumentDirectoryEntryIdentificationPart;

    /// <summary>BIAN <c>CorporateCustomerReference</c> (1.21), an <c>Involvedparty</c> object.</summary>
    public const string CorporateCustomerReference = "CorporateCustomerReference";

    /// <summary>BIAN <c>CorporateCustomerLegalEntityReference</c> (1.21), an <c>Involvedparty</c> object.</summary>
    public const string CorporateCustomerLegalEntityReference = "CorporateCustomerLegalEntityReference";

    /// <summary>BIAN <c>CompanyOfficerReference</c> (1.21), an <c>Involvedparty</c> object.</summary>
    public const string CompanyOfficerReference = "CompanyOfficerReference";

    /// <summary>BIAN <c>CompanyOfficerRole</c> (1.21), read as the role text (ADR 0034).</summary>
    public const string CompanyOfficerRole = "CompanyOfficerRole";

    /// <summary>The part of a BIAN <c>Involvedparty</c> object RimBank records: the referenced directory entry.</summary>
    public const string PartyReferencePart = "PartyReference";

    /// <summary>The part of a BIAN <c>Involvedparty</c> object RimBank refuses: the role is in <see cref="CompanyOfficerRole"/>.</summary>
    public const string InvolvementReferencePart = "InvolvementReference";

    /// <summary>BIAN <c>CorporateCustomerReference.PartyReference</c>.</summary>
    public const string CorporateCustomerPartyReference = CorporateCustomerReference + "." + PartyReferencePart;

    /// <summary>BIAN <c>CorporateCustomerLegalEntityReference.PartyReference</c>.</summary>
    public const string LegalEntityPartyReference = CorporateCustomerLegalEntityReference + "." + PartyReferencePart;

    /// <summary>BIAN <c>CompanyOfficerReference.PartyReference</c>.</summary>
    public const string CompanyOfficerPartyReference = CompanyOfficerReference + "." + PartyReferencePart;

    /// <summary>The only part of a BIAN <c>Country</c> object RimBank records.</summary>
    public const string CountryCodePart = "Code";

    /// <summary>The part of a BIAN <c>Status</c> object that holds the residency code.</summary>
    public const string StatusDescriptionPart = "StatusDescription";

    /// <summary>The part of a BIAN <c>Status</c> object that holds when the status was assigned.</summary>
    public const string StatusDateTimePart = "StatusDateTime";

    /// <summary>
    /// Every top-level BIAN <c>UpdateReference</c> field RimBank does not record yet, with the roadmap phase that will
    /// record it (ADR 0029). Empty since 1.21, which recorded the last four (ADR 0034); a later BIAN release that adds a
    /// field stages it here.
    /// </summary>
    public static FrozenDictionary<string, string> Staged { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);
}
