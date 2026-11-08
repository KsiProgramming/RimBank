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
public sealed record PartyReferenceInput(
    string? TaxIdentificationNumber = null,
    string? NationalRegistrationNumber = null,
    string? LegalEntityIdentifier = null,
    string? PartyFullLegalName = null,
    string? PartyNameSalutation = null,
    string? DateofBirth = null,
    PartyCountryInput? Nationality = null,
    PartyCountryInput? PartyCountryOfResidence = null,
    ResidencyStatusInput? ResidencyStatus = null)
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
