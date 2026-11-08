using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Associate a party with another party of the directory: BIAN <c>Associations/Register</c> (ADR 0024, ADR 0033).
/// Handled by <see cref="RegisterPartyAssociationHandler"/>, which acknowledges with <see cref="PartyAssociationAcknowledged"/>.
/// </summary>
/// <param name="OwnerId">The entry that owns the association: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="Terms">The associate, type and validity period, built by <see cref="PartyAssociationTerms.Create"/>.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record RegisterPartyAssociation(
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyAssociationTerms Terms,
    IdempotencyKey IdempotencyKey) : ICommand;
