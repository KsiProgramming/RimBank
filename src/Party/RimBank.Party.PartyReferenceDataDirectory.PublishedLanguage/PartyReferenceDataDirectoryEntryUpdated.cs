namespace RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

/// <summary>
/// A party's directory entry was updated: BIAN channel <c>PartyReferenceDataDirectoryEntry/Updated</c>, message
/// identity <c>party-reference-data-directory-entry.updated.v1</c> (ADR 0036). It carries the Control Record's state
/// after the update, so a consumer needs no callback. Fields are only ever added, never removed, renamed or retyped.
/// </summary>
/// <param name="EventId">This event's own id; a redelivery carries the same one.</param>
/// <param name="OccurredAt">When the entry was updated.</param>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry.</param>
/// <param name="Version">The entry's version after this event: consumers order and deduplicate on it.</param>
/// <param name="PartyType">A <see cref="PartyTypes"/> value: the entry's party type from now on.</param>
/// <param name="DirectoryEntryDateType">Always <see cref="DirectoryEntryDateTypes.RefreshDate"/>.</param>
/// <param name="DirectoryEntryDate">The entry's refresh date.</param>
public sealed record PartyReferenceDataDirectoryEntryUpdated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PartyReferenceDataDirectoryEntryId,
    long Version,
    string PartyType,
    string DirectoryEntryDateType,
    DateTimeOffset DirectoryEntryDate)
{
    /// <summary>The BIAN AsyncAPI channel this event is published on.</summary>
    public const string Channel = "PartyReferenceDataDirectoryEntry/Updated";

    /// <summary>The stable message identity on the wire; a breaking change publishes a new type with a new identity.</summary>
    public const string MessageIdentity = "party-reference-data-directory-entry.updated.v1";
}
