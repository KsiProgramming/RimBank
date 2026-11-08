using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of every successful Reference operation: the BIAN Reference fields RimBank records (ADR 0029) and the ids of
/// the entry and its Reference. Update builds it from its acknowledgement, Retrieve from the entry's view (tech-stack §5).
/// Unknown identifiers, and objects with none, are left out, and the entry's version is sent as the <c>ETag</c> header,
/// not in the body.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> of its operations.</param>
/// <param name="ReferenceId">The entry's Reference: the <c>{referenceid}</c> of its operations.</param>
/// <param name="GovernmentIssuedIdentityReference">BIAN <c>GovernmentIssuedIdentityReference</c>.</param>
/// <param name="PartyLegalEntityReference">BIAN <c>PartyLegalEntityReference</c>, with the LEI.</param>
public sealed record PartyReferenceResponse(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("GovernmentIssuedIdentityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GovernmentIssuedIdentityModel? GovernmentIssuedIdentityReference,
    [property: JsonPropertyName("PartyLegalEntityReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PartyLegalEntityModel? PartyLegalEntityReference)
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
            });
    }
}
