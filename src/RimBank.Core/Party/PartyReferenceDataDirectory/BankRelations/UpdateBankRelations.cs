using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Update the party's BankRelations: BIAN <c>BankRelations/Update</c> (ADR 0024, ADR 0035), a partial update. Handled by
/// <see cref="UpdateBankRelationsHandler"/>, which acknowledges with <see cref="UpdateBankRelationsAcknowledged"/>.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="BankRelationsId">The entry's BankRelations: the <c>{bankrelationsid}</c> route value.</param>
/// <param name="ExpectedVersion">
/// The entry's stream version the caller last read (its <c>If-Match</c>, ADR 0027). Any other current version is
/// <c>party.version-conflict</c>.
/// </param>
/// <param name="Changes">The changes, built by <see cref="BankRelationsChanges.Create"/>.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record UpdateBankRelations(
    PartyReferenceDataDirectoryEntryId EntryId,
    BankRelationsId BankRelationsId,
    long ExpectedVersion,
    BankRelationsChanges Changes,
    IdempotencyKey IdempotencyKey) : ICommand;
