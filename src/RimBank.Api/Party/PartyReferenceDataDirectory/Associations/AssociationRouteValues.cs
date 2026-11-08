using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>Reads the ids the Associations routes carry, entry first (roadmap 1.11).</summary>
internal static class AssociationRouteValues
{
    /// <summary>The owner's entry id, or <c>shared.invalid-identifier</c> naming <c>partyreferencedatadirectoryid</c>.</summary>
    public static Result<PartyReferenceDataDirectoryEntryId> ReadOwner(string partyreferencedatadirectoryid) =>
        PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid) switch
        {
            { IsFailure: true } => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            var ownerId => ownerId.Value,
        };

    /// <summary>
    /// The owner's entry id and the association id, or <c>shared.invalid-identifier</c> naming
    /// <c>partyreferencedatadirectoryid</c> or <c>associationsid</c>.
    /// </summary>
    public static Result<(PartyReferenceDataDirectoryEntryId OwnerId, PartyAssociationId AssociationId)> Read(
        string partyreferencedatadirectoryid,
        string associationsid) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid), PartyAssociationId.TryCreate(associationsid)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => PartyAssociationErrors.AssociationIdInvalid,
            var (ownerId, associationId) => (ownerId.Value, associationId.Value),
        };
}
