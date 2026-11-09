namespace RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

/// <summary>
/// A party was registered in the directory: BIAN channel <c>PartyReferenceDataDirectoryEntry/Created</c>, message
/// identity <c>party-reference-data-directory-entry.created.v1</c> (ADR 0036). It carries the Control Record's state,
/// so a consumer needs no callback. Fields are only ever added, never removed, renamed or retyped.
/// </summary>
/// <param name="EventId">This event's own id; a redelivery carries the same one.</param>
/// <param name="OccurredAt">When the party was registered.</param>
/// <param name="PartyReferenceDataDirectoryEntryId">The new entry.</param>
/// <param name="Version">The entry's version after this event (1): consumers order and deduplicate on it.</param>
/// <param name="PartyType">A <see cref="PartyTypes"/> value.</param>
/// <param name="DirectoryEntryDateType">Always <see cref="DirectoryEntryDateTypes.OpenDate"/>.</param>
/// <param name="DirectoryEntryDate">The entry's open date.</param>
public sealed record PartyReferenceDataDirectoryEntryCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PartyReferenceDataDirectoryEntryId,
    long Version,
    string PartyType,
    string DirectoryEntryDateType,
    DateTimeOffset DirectoryEntryDate)
{
    /// <summary>The BIAN AsyncAPI channel this event is published on.</summary>
    public const string Channel = "PartyReferenceDataDirectoryEntry/Created";

    /// <summary>The stable message identity on the wire; a breaking change publishes a new type with a new identity.</summary>
    public const string MessageIdentity = "party-reference-data-directory-entry.created.v1";
}
