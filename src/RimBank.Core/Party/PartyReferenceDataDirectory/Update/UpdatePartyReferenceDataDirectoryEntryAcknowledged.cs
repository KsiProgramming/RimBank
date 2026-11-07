namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The acknowledgement of <see cref="UpdatePartyReferenceDataDirectoryEntry"/>: the entry as the Update decision left
/// it, never a read model (tech-stack §5 and §8.1). The idempotency policy stores it and returns it again for a
/// repeated key (ADR 0018).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="Version">The entry's stream version after the Update, which the next command sends back as <c>If-Match</c>.</param>
/// <param name="PartyType">The entry's party type after the Update.</param>
/// <param name="OpenDate">The entry's <c>DirectoryEntryDate</c> of type <c>OpenDate</c>.</param>
/// <param name="RefreshDate">The entry's <c>DirectoryEntryDate</c> of type <c>RefreshDate</c>: the time of this Update.</param>
/// <param name="DemographicsId">The entry's Demographics Behavior Qualifier.</param>
/// <param name="PartyReferenceId">The entry's Reference Behavior Qualifier.</param>
/// <param name="BankRelationsId">The entry's BankRelations Behavior Qualifier.</param>
public sealed record UpdatePartyReferenceDataDirectoryEntryAcknowledged(
    PartyReferenceDataDirectoryEntryId EntryId,
    long Version,
    PartyType PartyType,
    DateTimeOffset OpenDate,
    DateTimeOffset RefreshDate,
    DemographicsId DemographicsId,
    PartyReferenceId PartyReferenceId,
    BankRelationsId BankRelationsId);
