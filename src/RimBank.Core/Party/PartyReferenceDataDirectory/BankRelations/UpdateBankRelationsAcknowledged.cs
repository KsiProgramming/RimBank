namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The acknowledgement of <see cref="UpdateBankRelations"/>: the entry's BankRelations as the Update decision left it,
/// never a read model (tech-stack §5 and §8.1). The idempotency policy stores it and returns it again for a repeated key
/// (ADR 0018).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="BankRelationsId">The entry's BankRelations.</param>
/// <param name="Version">The entry's stream version after the Update, which the next command sends back as <c>If-Match</c>.</param>
/// <param name="BankRelations">The BankRelations after the Update.</param>
public sealed record UpdateBankRelationsAcknowledged(
    PartyReferenceDataDirectoryEntryId EntryId,
    BankRelationsId BankRelationsId,
    long Version,
    BankRelationsDetails BankRelations);
