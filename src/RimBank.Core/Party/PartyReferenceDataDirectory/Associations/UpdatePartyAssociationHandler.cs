using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdatePartyAssociation"/> on the write side (tech-stack §8.1, ADR 0024, ADR 0027).</summary>
public static class UpdatePartyAssociationHandler
{
    /// <summary>
    /// Loads the association; checks that it exists and is owned by the caller's entry, then that the caller's expected
    /// version is current; decides the Update and appends it with that expected version.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.association-not-found</c>,
    /// <c>party.version-conflict</c> or the decision's error.
    /// </returns>
    public static async Task<Result<PartyAssociationAcknowledged>> HandleAsync(
        UpdatePartyAssociation command,
        IPartyDirectory directory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);

        var history = await directory.LoadAssociationAsync(command.AssociationId, cancellationToken);
        var state = history.Aggregate(PartyAssociation.Initial, PartyAssociation.Evolve);

        return (state.IsRegistered && state.OwnerId == command.OwnerId, history.Count == command.ExpectedVersion) switch
        {
            (false, _) => PartyAssociationErrors.AssociationNotFound,
            (true, false) => PartyAssociationErrors.VersionConflict,
            (true, true) => PartyAssociation
                .Decide(new UpdatePartyAssociationDetails(command.OwnerId, command.Changes), state)
                .Map(events =>
                {
                    directory.AppendToAssociation(command.AssociationId, command.ExpectedVersion, events);
                    return PartyAssociationAcknowledged.From(
                        events.Aggregate(state, PartyAssociation.Evolve),
                        history.Count + events.Count);
                }),
        };
    }
}
