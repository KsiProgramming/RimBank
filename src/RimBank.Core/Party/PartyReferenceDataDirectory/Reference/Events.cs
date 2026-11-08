namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The entry's Reference was updated (BIAN <c>Reference/Update</c>). It carries the full Reference after the update,
/// so a consumer never needs the earlier events to know it (tech-stack §8.4).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="PartyReferenceId">The entry's Reference.</param>
/// <param name="Details">The Reference after the update.</param>
public sealed record PartyReferenceUpdated(
    PartyReferenceDataDirectoryEntryId EntryId,
    PartyReferenceId PartyReferenceId,
    PartyReferenceDetails Details) : PartyReferenceDataDirectoryEntryEvent;
