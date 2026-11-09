using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of <c>party_reference_data_directory_update</c>: the fields of the HTTP Update response with BIAN's names,
/// which it mirrors (a parity test proves the JSON is equal, ADR 0039), and the entry's new <see cref="Version"/>, which
/// HTTP sends as the <c>ETag</c> header. It is built from the command's acknowledgement, never from a read model.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry.</param>
/// <param name="PartyType">BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c>.</param>
/// <param name="DirectoryEntryDateType">BIAN <c>DirectoryEntryDateType</c>: <c>OpenDate</c>.</param>
/// <param name="DirectoryEntryDate">BIAN <c>DirectoryEntryDate</c>: when the directory registered the party.</param>
/// <param name="RefreshDate">When this update was recorded.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="ReferenceId">The entry's Reference.</param>
/// <param name="BankRelationsId">The entry's BankRelations.</param>
/// <param name="Version">The entry's new version: the <c>expectedVersion</c> of the next command (ADR 0027).</param>
public sealed record UpdatePartyReferenceDataDirectoryEntryResult(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("PartyType")] string PartyType,
    [property: JsonPropertyName("DirectoryEntryDateType")] string DirectoryEntryDateType,
    [property: JsonPropertyName("DirectoryEntryDate")] DateTimeOffset DirectoryEntryDate,
    [property: JsonPropertyName("RefreshDate")] DateTimeOffset RefreshDate,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The <c>DirectoryEntryDateType</c> of the entry's open date.</summary>
    public const string OpenDate = "OpenDate";

    /// <summary>The result for <paramref name="acknowledged"/>.</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdatePartyReferenceDataDirectoryEntry"/>.</param>
    /// <returns>The tool's result.</returns>
    public static UpdatePartyReferenceDataDirectoryEntryResult From(UpdatePartyReferenceDataDirectoryEntryAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);

        return new UpdatePartyReferenceDataDirectoryEntryResult(
            acknowledged.EntryId.Value,
            acknowledged.PartyType.ToString(),
            OpenDate,
            acknowledged.OpenDate,
            acknowledged.RefreshDate,
            acknowledged.DemographicsId.Value,
            acknowledged.PartyReferenceId.Value,
            acknowledged.BankRelationsId.Value,
            acknowledged.Version);
    }
}
