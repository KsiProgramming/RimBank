using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Retrieve an association: BIAN <c>Associations/Retrieve</c> (ADR 0024). Handled by
/// <see cref="RetrievePartyAssociationHandler"/>, which reads the association's view.
/// </summary>
/// <param name="OwnerId">The entry the caller named: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="AssociationId">The association: the <c>{associationsid}</c> route value.</param>
public sealed record RetrievePartyAssociation(PartyReferenceDataDirectoryEntryId OwnerId, PartyAssociationId AssociationId)
    : IQuery<PartyAssociationView>;
