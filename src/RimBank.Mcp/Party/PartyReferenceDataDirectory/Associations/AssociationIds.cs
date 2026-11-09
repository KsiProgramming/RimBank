using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>Reads the owner and association ids of an Associations tool, with the errors HTTP gives for its route values.</summary>
internal static class AssociationIds
{
    /// <summary>The owner's id, or the error of an invalid one.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The owner's entry id, as sent.</param>
    /// <returns>The typed id, or the validation error.</returns>
    public static Result<PartyReferenceDataDirectoryEntryId> ReadOwner(string partyReferenceDataDirectoryEntryId) =>
        PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId) switch
        {
            { IsFailure: true } => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            var ownerId => ownerId.Value,
        };

    /// <summary>The two ids, or the error of the first invalid one.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The owner's entry id, as sent.</param>
    /// <param name="associationsId">The association's id, as sent.</param>
    /// <returns>The typed ids, or the validation error.</returns>
    public static Result<(PartyReferenceDataDirectoryEntryId OwnerId, PartyAssociationId AssociationId)> Read(
        string partyReferenceDataDirectoryEntryId,
        string associationsId) =>
        (PartyReferenceDataDirectoryEntryId.TryCreate(partyReferenceDataDirectoryEntryId), PartyAssociationId.TryCreate(associationsId)) switch
        {
            ({ IsFailure: true }, _) => PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid,
            (_, { IsFailure: true }) => PartyAssociationErrors.AssociationIdInvalid,
            var (ownerId, associationId) => (ownerId.Value, associationId.Value),
        };
}
