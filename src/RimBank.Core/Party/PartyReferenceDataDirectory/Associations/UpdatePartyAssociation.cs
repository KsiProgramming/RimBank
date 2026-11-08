using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Update an association: BIAN <c>Associations/Update</c> (ADR 0024, ADR 0033), a partial update of its type and validity
/// period. Handled by <see cref="UpdatePartyAssociationHandler"/>, which acknowledges with
/// <see cref="PartyAssociationAcknowledged"/>.
/// </summary>
/// <param name="OwnerId">The entry the caller named: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="AssociationId">The association: the <c>{associationsid}</c> route value.</param>
/// <param name="ExpectedVersion">
/// The association's stream version the caller last read (its <c>If-Match</c>, ADR 0027). Any other current version is
/// <c>party.version-conflict</c>.
/// </param>
/// <param name="Changes">The changes, built by <see cref="PartyAssociationChanges.Create"/>.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record UpdatePartyAssociation(
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyAssociationId AssociationId,
    long ExpectedVersion,
    PartyAssociationChanges Changes,
    IdempotencyKey IdempotencyKey) : ICommand;
