using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of <c>party_reference_data_directory_retrieve</c>: the fields of the HTTP Retrieve response with BIAN's
/// names, which it mirrors (a parity test proves the JSON is equal, ADR 0039), and the entry's <see cref="Version"/>,
/// which HTTP sends as the <c>ETag</c> header. It is built only from the entry's view (tech-stack §5).
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry.</param>
/// <param name="PartyType">BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c>.</param>
/// <param name="DirectoryEntryDateType">BIAN <c>DirectoryEntryDateType</c>: <c>OpenDate</c>, the date of <c>DirectoryEntryDate</c>.</param>
/// <param name="DirectoryEntryDate">BIAN <c>DirectoryEntryDate</c>: when the directory registered the party.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="ReferenceId">The entry's Reference.</param>
/// <param name="BankRelationsId">The entry's BankRelations.</param>
/// <param name="RefreshDate">The entry's last Update. Omitted until the first one.</param>
/// <param name="Version">The entry's version: the <c>expectedVersion</c> of a later command (ADR 0027).</param>
public sealed record RetrievePartyReferenceDataDirectoryEntryResult(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("PartyType")] string PartyType,
    [property: JsonPropertyName("DirectoryEntryDateType")] string DirectoryEntryDateType,
    [property: JsonPropertyName("DirectoryEntryDate")] DateTimeOffset DirectoryEntryDate,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId,
    [property: JsonPropertyName("RefreshDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? RefreshDate,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The <c>DirectoryEntryDateType</c> of the entry's open date.</summary>
    public const string OpenDate = "OpenDate";

    /// <summary>The result for <paramref name="view"/>.</summary>
    /// <param name="view">The entry's read model.</param>
    /// <returns>The tool's result.</returns>
    public static RetrievePartyReferenceDataDirectoryEntryResult From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new RetrievePartyReferenceDataDirectoryEntryResult(
            view.Id,
            view.PartyType.ToString(),
            OpenDate,
            view.OpenDate,
            view.DemographicsId.Value,
            view.PartyReferenceId.Value,
            view.BankRelationsId.Value,
            view.RefreshDate,
            view.Version);
    }
}
