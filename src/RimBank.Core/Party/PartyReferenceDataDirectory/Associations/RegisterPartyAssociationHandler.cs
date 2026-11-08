using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="RegisterPartyAssociation"/> on the write side (tech-stack §8.1, ADR 0024).</summary>
public static class RegisterPartyAssociationHandler
{
    /// <summary>
    /// Creates the association id; loads the owner's and the associate's entry streams through the write port, never a
    /// view (ADR 0024, cross-aggregate rule); decides Register and starts the association's stream only when the decision
    /// accepts.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="time">The clock for the new id.</param>
    /// <param name="cancellationToken">Cancels the loads.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, or <c>party.party-not-found</c>, <c>party.self-association</c>,
    /// <c>party.associate-not-registered</c> or another decision error.
    /// </returns>
    public static async Task<Result<PartyAssociationAcknowledged>> HandleAsync(
        RegisterPartyAssociation command,
        IPartyDirectory directory,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(time);

        var associationId = PartyAssociationId.New(time);
        var owner = await IsRegisteredAsync(directory, command.OwnerId, cancellationToken);
        var associate = command.Terms.Associate == command.OwnerId
            ? owner
            : await IsRegisteredAsync(directory, command.Terms.Associate, cancellationToken);
        var history = await directory.LoadAssociationAsync(associationId, cancellationToken);
        var state = history.Aggregate(PartyAssociation.Initial, PartyAssociation.Evolve);

        return PartyAssociation
            .Decide(new RegisterPartyAssociationDetails(associationId, command.OwnerId, command.Terms, owner, associate), state)
            .Map(events =>
            {
                directory.StartAssociation(associationId, events);
                return PartyAssociationAcknowledged.From(
                    events.Aggregate(state, PartyAssociation.Evolve),
                    history.Count + events.Count);
            });
    }

    private static async Task<bool> IsRegisteredAsync(
        IPartyDirectory directory,
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken)
    {
        var history = await directory.LoadEntryAsync(entryId, cancellationToken);
        return history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve).IsRegistered;
    }
}
