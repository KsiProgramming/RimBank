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

    /// <summary>
    /// Every top-level BIAN <c>UpdateReference</c> field RimBank does not record yet, with the roadmap phase that will
    /// record it (ADR 0029).
    /// </summary>
    public static FrozenDictionary<string, string> Staged { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PartyFullLegalName"] = "1.18",
        ["PartyNameSalutation"] = "1.18",
        ["DateofBirth"] = "1.18",
        ["Nationality"] = "1.18",
        ["PartyCountryOfResidence"] = "1.18",
        ["ResidencyStatus"] = "1.18",
        ["ResidentialAddress"] = "1.19",
        ["PartyAddress"] = "1.19",
        ["CorporateAddress"] = "1.19",
        ["eMailAddress"] = "1.19",
        ["CellPhoneNumber"] = "1.19",
        ["SocialNetworkContacts"] = "1.19",
        ["PartyLegalStructure"] = "1.20",
        ["PoliticalExposureType"] = "1.20",
        ["PoliticalExposureDescriptionRecord"] = "1.20",
        ["CustomerSinceDate"] = "1.20",
        ["CorporateCustomerReference"] = "1.20",
        ["CorporateCustomerLegalEntityReference"] = "1.20",
        ["CompanyOfficerReference"] = "1.20",
        ["CompanyOfficerRole"] = "1.20",
        ["GovernmentIssuedDocumentDetails"] = "1.20",
        ["DocumentDirectoryEntryInstanceReference"] = "1.20",
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
