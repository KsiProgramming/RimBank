namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The entry's BankRelations was updated (BIAN <c>BankRelations/Update</c>, roadmap 1.12). It carries the full
/// BankRelations after the update, so a consumer never needs the earlier events to know it (tech-stack §8.4).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="BankRelationsId">The entry's BankRelations.</param>
/// <param name="Details">The BankRelations after the update.</param>
public sealed record BankRelationsUpdated(
    PartyReferenceDataDirectoryEntryId EntryId,
    BankRelationsId BankRelationsId,
    BankRelationsDetails Details) : PartyReferenceDataDirectoryEntryEvent;
