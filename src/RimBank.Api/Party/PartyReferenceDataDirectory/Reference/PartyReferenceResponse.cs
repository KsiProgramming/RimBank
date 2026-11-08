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
    [property: JsonPropertyName("ResidencyStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ResidencyStatusModel? ResidencyStatus = null)
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
            });
    }

    private static CountryModel? CountryOf(SharedKernel.CountryCode? country) =>
        country switch
        {
            null => null,
            { } code => new CountryModel { Code = code.Code },
        };
}
