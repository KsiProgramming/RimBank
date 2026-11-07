using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Update the details of a directory entry: BIAN <c>PartyReferenceDataDirectoryEntry/Update</c> (ADR 0024). Handled by
/// <see cref="UpdatePartyReferenceDataDirectoryEntryHandler"/>, which acknowledges with
/// <see cref="UpdatePartyReferenceDataDirectoryEntryAcknowledged"/>.
/// </summary>
/// <param name="EntryId">The entry to update.</param>
/// <param name="ExpectedVersion">
/// The entry's stream version the caller last read (its <c>If-Match</c>, ADR 0027). Any other current version is
/// <c>party.version-conflict</c>.
/// </param>
/// <param name="PartyType">The new party type, or <c>null</c> to keep the current one.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record UpdatePartyReferenceDataDirectoryEntry(
    PartyReferenceDataDirectoryEntryId EntryId,
    long ExpectedVersion,
    PartyType? PartyType,
    IdempotencyKey IdempotencyKey) : ICommand;
