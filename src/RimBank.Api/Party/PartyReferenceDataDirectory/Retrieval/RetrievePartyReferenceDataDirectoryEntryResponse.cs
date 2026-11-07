using System.Text.Json.Serialization;
using RimBank.Party.PartyReferenceDataDirectory.Retrieval;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Retrieval;

/// <summary>
/// The body of a successful Retrieve: BIAN's <c>RetrievePartyReferenceDataDirectoryEntry</c> fields and the ids of the
/// entry and its Behavior Qualifiers, in the same shape as the Register response (ADR 0024). It is built only from the
/// entry's view (tech-stack §5). The entry's version is sent as the <c>ETag</c> header, not in the body. BIAN's
/// <c>PartyReference</c> is not returned until the directory stores it (roadmap 1.9, 1.10).
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> of its operations.</param>
/// <param name="PartyType">BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c>.</param>
/// <param name="DirectoryEntryDateType">BIAN <c>DirectoryEntryDateType</c>: <c>OpenDate</c>, the only date the entry records.</param>
/// <param name="DirectoryEntryDate">BIAN <c>DirectoryEntryDate</c>: when the directory registered the party.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> of its operations.</param>
/// <param name="ReferenceId">The entry's Reference: the <c>{referenceid}</c> of its operations.</param>
/// <param name="BankRelationsId">The entry's BankRelations: the <c>{bankrelationsid}</c> of its operations.</param>
public sealed record RetrievePartyReferenceDataDirectoryEntryResponse(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("PartyType")] string PartyType,
    [property: JsonPropertyName("DirectoryEntryDateType")] string DirectoryEntryDateType,
    [property: JsonPropertyName("DirectoryEntryDate")] DateTimeOffset DirectoryEntryDate,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId)
{
    /// <summary>The <c>DirectoryEntryDateType</c> of the entry's open date.</summary>
    public const string OpenDate = "OpenDate";

    /// <summary>The response for <paramref name="view"/>.</summary>
    /// <param name="view">The entry's read model.</param>
    /// <returns>The response body.</returns>
    public static RetrievePartyReferenceDataDirectoryEntryResponse From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new RetrievePartyReferenceDataDirectoryEntryResponse(
            view.Id,
            view.PartyType.ToString(),
            OpenDate,
            view.OpenDate,
            view.DemographicsId.Value,
            view.PartyReferenceId.Value,
            view.BankRelationsId.Value);
    }
}
