using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of a successful Update: the same BIAN fields and ids as the Register response, plus the entry's
/// <see cref="RefreshDate"/> (ADR 0024). It is built from the command's acknowledgement, never from a read model
/// (tech-stack §5). The entry's new version is sent as the <c>ETag</c> header, not in the body.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> of its operations.</param>
/// <param name="PartyType">BIAN <c>PartyType</c> after the Update: <c>Person</c> or <c>Organisation</c>.</param>
/// <param name="DirectoryEntryDateType">BIAN <c>DirectoryEntryDateType</c>: <c>OpenDate</c>, the date of <see cref="DirectoryEntryDate"/>.</param>
/// <param name="DirectoryEntryDate">BIAN <c>DirectoryEntryDate</c>: when the directory registered the party.</param>
/// <param name="RefreshDate">The entry's <c>DirectoryEntryDate</c> of type <c>RefreshDate</c>: the time of this Update.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> of its operations.</param>
/// <param name="ReferenceId">The entry's Reference: the <c>{referenceid}</c> of its operations.</param>
/// <param name="BankRelationsId">The entry's BankRelations: the <c>{bankrelationsid}</c> of its operations.</param>
public sealed record UpdatePartyReferenceDataDirectoryEntryResponse(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("PartyType")] string PartyType,
    [property: JsonPropertyName("DirectoryEntryDateType")] string DirectoryEntryDateType,
    [property: JsonPropertyName("DirectoryEntryDate")] DateTimeOffset DirectoryEntryDate,
    [property: JsonPropertyName("RefreshDate")] DateTimeOffset RefreshDate,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId)
{
    /// <summary>The <c>DirectoryEntryDateType</c> of <see cref="DirectoryEntryDate"/>.</summary>
    public const string OpenDate = "OpenDate";

    /// <summary>The response for <paramref name="acknowledged"/>.</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdatePartyReferenceDataDirectoryEntry"/>.</param>
    /// <returns>The response body.</returns>
    public static UpdatePartyReferenceDataDirectoryEntryResponse From(UpdatePartyReferenceDataDirectoryEntryAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);

        return new UpdatePartyReferenceDataDirectoryEntryResponse(
            acknowledged.EntryId.Value,
            acknowledged.PartyType.ToString(),
            OpenDate,
            acknowledged.OpenDate,
            acknowledged.RefreshDate,
            acknowledged.DemographicsId.Value,
            acknowledged.PartyReferenceId.Value,
            acknowledged.BankRelationsId.Value);
    }
}
