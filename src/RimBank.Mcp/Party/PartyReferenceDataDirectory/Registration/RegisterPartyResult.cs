using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of <c>party_reference_data_directory_register</c>: the fields of the HTTP Register response with BIAN's
/// names, which it mirrors (a parity test proves the JSON is equal, ADR 0039), and the entry's <see cref="Version"/>,
/// which HTTP sends as the <c>ETag</c> header.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The new entry: the id of later operations.</param>
/// <param name="PartyType">BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c>.</param>
/// <param name="DirectoryEntryDateType">BIAN <c>DirectoryEntryDateType</c>: always <c>OpenDate</c> on Register.</param>
/// <param name="DirectoryEntryDate">BIAN <c>DirectoryEntryDate</c>: when the directory registered the party.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="ReferenceId">The entry's Reference.</param>
/// <param name="BankRelationsId">The entry's BankRelations.</param>
/// <param name="Version">The entry's version: the <c>expectedVersion</c> of a later command (ADR 0027).</param>
public sealed record RegisterPartyResult(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("PartyType")] string PartyType,
    [property: JsonPropertyName("DirectoryEntryDateType")] string DirectoryEntryDateType,
    [property: JsonPropertyName("DirectoryEntryDate")] DateTimeOffset DirectoryEntryDate,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The <c>DirectoryEntryDateType</c> of the date Register records.</summary>
    public const string OpenDate = "OpenDate";

    /// <summary>The result for <paramref name="acknowledged"/>.</summary>
    /// <param name="acknowledged">The outcome of <see cref="RegisterParty"/>.</param>
    /// <returns>The tool's result.</returns>
    public static RegisterPartyResult From(RegisterPartyAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);

        return new RegisterPartyResult(
            acknowledged.EntryId.Value,
            acknowledged.PartyType.ToString(),
            OpenDate,
            acknowledged.OpenDate,
            acknowledged.DemographicsId.Value,
            acknowledged.PartyReferenceId.Value,
            acknowledged.BankRelationsId.Value,
            acknowledged.Version);
    }
}
