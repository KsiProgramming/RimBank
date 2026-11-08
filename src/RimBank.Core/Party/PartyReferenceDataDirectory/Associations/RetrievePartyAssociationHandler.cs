using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="RetrievePartyAssociation"/> on the read side (tech-stack §8.2, ADR 0024).</summary>
public static class RetrievePartyAssociationHandler
{
    /// <summary>Reads the association's view through the read port and checks that the caller's entry owns it.</summary>
    /// <param name="query">The query.</param>
    /// <param name="enquiry">The context's read port.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The association's view, or <c>party.association-not-found</c> when it does not exist or has another owner.</returns>
    public static async Task<Result<PartyAssociationView>> HandleAsync(
        RetrievePartyAssociation query,
        IPartyDirectoryEnquiry enquiry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(enquiry);

        return await enquiry.FindAssociationAsync(query.AssociationId, cancellationToken) switch
        {
            { } view when view.OwnerId == query.OwnerId => view,
            _ => PartyAssociationErrors.AssociationNotFound,
        };
    }
}
