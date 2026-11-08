namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The Reference values a caller sent in a BIAN <c>Reference/Update</c>, as text, before
/// <see cref="PartyReferenceChanges.Create"/> reads them. For each value, <c>null</c> keeps the current one, and a blank
/// one (or an object whose parts are all blank) clears it. Every adapter (HTTP, MCP) fills it from its own transport.
/// </summary>
/// <param name="TaxIdentificationNumber">BIAN <c>GovernmentIssuedIdentityReference.TaxIdentificationNumber</c>.</param>
/// <param name="NationalRegistrationNumber">BIAN <c>GovernmentIssuedIdentityReference.NationalRegistrationNumber</c>.</param>
/// <param name="LegalEntityIdentifier">BIAN <c>PartyLegalEntityReference.Identification</c>: the LEI.</param>
/// <param name="PartyFullLegalName">BIAN <c>PartyFullLegalName</c>.</param>
/// <param name="PartyNameSalutation">BIAN <c>PartyNameSalutation</c>.</param>
/// <param name="DateofBirth">BIAN <c>DateofBirth</c>: an ISO 8601 date or date-time.</param>
/// <param name="Nationality">BIAN <c>Nationality</c>.</param>
/// <param name="PartyCountryOfResidence">BIAN <c>PartyCountryOfResidence</c>.</param>
/// <param name="ResidencyStatus">BIAN <c>ResidencyStatus</c>.</param>
/// <param name="ResidentialAddress">BIAN <c>ResidentialAddress</c> (1.19).</param>
/// <param name="PartyAddress">BIAN <c>PartyAddress</c> (1.19).</param>
/// <param name="CorporateAddress">BIAN <c>CorporateAddress</c> (1.19).</param>
/// <param name="EmailAddress">
/// BIAN <c>eMailAddress.EmailAddress</c> (1.19, ADR 0031). An <c>eMailAddress</c> object with no value is sent as blank.
/// </param>
/// <param name="MobileNumber">BIAN <c>CellPhoneNumber.MobileNumber</c> (1.19, ADR 0031).</param>
/// <param name="SocialNetworkUrl">BIAN <c>SocialNetworkContacts.URLAddress</c> (1.19, ADR 0031).</param>
/// <param name="PartyLegalStructure">BIAN <c>PartyLegalStructure</c> (1.20, ADR 0032), for example <c>Co-Operative</c>.</param>
/// <param name="PoliticalExposureType">BIAN <c>PoliticalExposureType</c> (1.20).</param>
/// <param name="PoliticalExposureDescription">BIAN <c>PoliticalExposureDescriptionRecord</c> (1.20).</param>
/// <param name="CustomerSinceDate">BIAN <c>CustomerSinceDate</c> (1.20): an ISO 8601 date or date-time.</param>
/// <param name="GovernmentIssuedDocument">BIAN <c>GovernmentIssuedDocumentDetails</c> (1.20).</param>
/// <param name="DocumentDirectoryEntryIdentification">
/// BIAN <c>DocumentDirectoryEntryInstanceReference.DocumentDirectoryEntryIdentification</c> (1.20). An object with no
/// value is sent as blank.
/// </param>
public sealed record PartyReferenceInput(
    string? TaxIdentificationNumber = null,
    string? NationalRegistrationNumber = null,
    string? LegalEntityIdentifier = null,
    string? PartyFullLegalName = null,
    string? PartyNameSalutation = null,
    string? DateofBirth = null,
    PartyCountryInput? Nationality = null,
    PartyCountryInput? PartyCountryOfResidence = null,
    ResidencyStatusInput? ResidencyStatus = null,
    PostalAddressInput? ResidentialAddress = null,
    PostalAddressInput? PartyAddress = null,
    PostalAddressInput? CorporateAddress = null,
    string? EmailAddress = null,
    string? MobileNumber = null,
    string? SocialNetworkUrl = null,
    string? PartyLegalStructure = null,
    string? PoliticalExposureType = null,
    string? PoliticalExposureDescription = null,
    string? CustomerSinceDate = null,
    PartyDocumentInput? GovernmentIssuedDocument = null,
    string? DocumentDirectoryEntryIdentification = null)
{
    /// <summary>Nothing sent: every value is kept.</summary>
    public static PartyReferenceInput None { get; } = new();
}

/// <summary>
/// A BIAN <c>Country</c> object as a caller sent it (roadmap 1.18). Only its ISO 3166 <c>Code</c> is read. Adapters report
/// its other parts as unsupported fields.
/// </summary>
/// <param name="Code">The code sent, in any case. Blank or missing clears the country.</param>
public sealed record PartyCountryInput(string? Code);

/// <summary>
/// A BIAN <c>ResidencyStatus</c> (ISO 20022 <c>Status</c>) object as a caller sent it (roadmap 1.18). Only its
/// <c>StatusDescription</c> and <c>StatusDateTime</c> are read. Adapters report its other parts as unsupported fields.
/// Both blank or missing clears the status.
/// </summary>
/// <param name="StatusDescription">The code sent: <c>Resident</c> or <c>NonResident</c>, in any case.</param>
/// <param name="StatusDateTime">When the status was assigned: an ISO 8601 date-time, or <c>null</c>.</param>
public sealed record ResidencyStatusInput(string? StatusDescription, string? StatusDateTime);

/// <summary>
/// A BIAN <c>PostalAddress</c> object as a caller sent it (roadmap 1.19, ADR 0031): the parts RimBank records, as text.
/// Adapters report its other parts (<c>AddressType</c>, <c>Location</c>, …) and the other parts of its <c>Country</c> as
/// unsupported fields. An address whose parts are all blank or missing (<see cref="IsBlank"/>) clears the address.
/// </summary>
/// <param name="Department">BIAN <c>Department</c>.</param>
/// <param name="SubDepartment">BIAN <c>SubDepartment</c>.</param>
/// <param name="StreetName">BIAN <c>StreetName</c>.</param>
/// <param name="StreetBuildingIdentification">BIAN <c>StreetBuildingIdentification</c>.</param>
/// <param name="BuildingName">BIAN <c>BuildingName</c>.</param>
/// <param name="Floor">BIAN <c>Floor</c>.</param>
/// <param name="PostOfficeBox">BIAN <c>PostOfficeBox</c>.</param>
/// <param name="PostCodeIdentification">BIAN <c>PostCodeIdentification</c>.</param>
/// <param name="TownName">BIAN <c>TownName</c>.</param>
/// <param name="Country">BIAN <c>Country</c>, whose <c>Code</c> is read.</param>
public sealed record PostalAddressInput(
    string? Department = null,
    string? SubDepartment = null,
    string? StreetName = null,
    string? StreetBuildingIdentification = null,
    string? BuildingName = null,
    string? Floor = null,
    string? PostOfficeBox = null,
    string? PostCodeIdentification = null,
    string? TownName = null,
    PartyCountryInput? Country = null)
{
    /// <summary>Whether every part is blank or missing, so the address is cleared.</summary>
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(Department)
        && string.IsNullOrWhiteSpace(SubDepartment)
        && string.IsNullOrWhiteSpace(StreetName)
        && string.IsNullOrWhiteSpace(StreetBuildingIdentification)
        && string.IsNullOrWhiteSpace(BuildingName)
        && string.IsNullOrWhiteSpace(Floor)
        && string.IsNullOrWhiteSpace(PostOfficeBox)
        && string.IsNullOrWhiteSpace(PostCodeIdentification)
        && string.IsNullOrWhiteSpace(TownName)
        && string.IsNullOrWhiteSpace(Country?.Code);
}

/// <summary>
/// A BIAN <c>GovernmentIssuedDocumentDetails</c> (<c>PartyDocument</c>) object as a caller sent it (roadmap 1.20,
/// ADR 0032): the parts RimBank records, as text. Adapters report its other parts, at any depth, as unsupported fields. A
/// document whose parts are all blank or missing (<see cref="IsBlank"/>) clears the document.
/// </summary>
/// <param name="PartyDocumentType">BIAN <c>PartyDocumentType</c>.</param>
/// <param name="DocumentNumber">BIAN <c>DocumentReference.DocumentIdentification.IdentifierValue</c>.</param>
/// <param name="DocumentName">BIAN <c>DocumentReference.DocumentName</c>.</param>
/// <param name="ValidFrom">BIAN <c>DocumentReference.DocumentValidityPeriod.FromDateTime.DateTimeContent</c>.</param>
/// <param name="ValidTo">BIAN <c>DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent</c>.</param>
public sealed record PartyDocumentInput(
    string? PartyDocumentType = null,
    string? DocumentNumber = null,
    string? DocumentName = null,
    string? ValidFrom = null,
    string? ValidTo = null)
{
    /// <summary>Whether every part is blank or missing, so the document is cleared.</summary>
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(PartyDocumentType)
        && string.IsNullOrWhiteSpace(DocumentNumber)
        && string.IsNullOrWhiteSpace(DocumentName)
        && string.IsNullOrWhiteSpace(ValidFrom)
        && string.IsNullOrWhiteSpace(ValidTo);
}
