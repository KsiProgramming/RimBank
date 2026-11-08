using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's declared documents, held by the entry's Reference Behavior Qualifier (roadmap 1.20, ADR 0032). BIAN gives
/// one object per field, so each is a single current value, and an unknown one is <c>null</c>. Personal data (7.2).
/// </summary>
/// <param name="GovernmentIssuedDocument">BIAN <c>GovernmentIssuedDocumentDetails</c>.</param>
/// <param name="DocumentDirectoryEntry">BIAN <c>DocumentDirectoryEntryInstanceReference</c>.</param>
public sealed record PartyDocuments(
    GovernmentIssuedDocument? GovernmentIssuedDocument,
    DocumentDirectoryEntryReference? DocumentDirectoryEntry)
{
    /// <summary>No document recorded.</summary>
    public static PartyDocuments Empty { get; } = new(null, null);

    /// <summary>Whether any document is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The documents after a partial update: a given document replaces the whole document, an omitted one is kept.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting documents.</returns>
    public PartyDocuments Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyDocuments(
            changes.GovernmentIssuedDocument.ApplyTo(GovernmentIssuedDocument),
            changes.DocumentDirectoryEntry.ApplyTo(DocumentDirectoryEntry));
    }
}
