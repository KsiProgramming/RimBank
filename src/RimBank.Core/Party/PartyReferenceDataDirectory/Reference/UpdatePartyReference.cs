using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Update the party's Reference: BIAN <c>Reference/Update</c> (ADR 0024, ADR 0029), a partial update of the party
/// identifiers. Handled by <see cref="UpdatePartyReferenceHandler"/>, which acknowledges with
/// <see cref="UpdatePartyReferenceAcknowledged"/>.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="PartyReferenceId">The entry's Reference: the <c>{referenceid}</c> route value.</param>
/// <param name="ExpectedVersion">
/// The entry's stream version the caller last read (its <c>If-Match</c>, ADR 0027). Any other current version is
/// <c>party.version-conflict</c>.
/// </param>
/// <param name="Changes">The changes, built by <see cref="PartyReferenceChanges.Create"/>.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record UpdatePartyReference(
    PartyReferenceDataDirectoryEntryId EntryId,
    PartyReferenceId PartyReferenceId,
    long ExpectedVersion,
    PartyReferenceChanges Changes,
    IdempotencyKey IdempotencyKey) : ICommand;
