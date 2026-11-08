using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Projects an association's events into its <see cref="PartyAssociationView"/> (tech-stack §8.2, ADR 0024). Pure: run
/// inline by Marten in the context's <c>.Postgresql</c> adapter, and by the core's tests without a database.
/// </summary>
/// <remarks>
/// Every event of the association's stream bumps <see cref="PartyAssociationView.Version"/>, so the view's version is
/// always the stream version. An event before <see cref="PartyAssociationRegistered"/> leaves no view.
/// </remarks>
public sealed class PartyAssociationViewProjection : IProjection<PartyAssociationView, PartyAssociationEvent>
{
    /// <inheritdoc />
    public static PartyAssociationView? Project(PartyAssociationView? view, PartyAssociationEvent domainEvent) =>
        (view, domainEvent) switch
        {
            (_, PartyAssociationRegistered registered) => new PartyAssociationView(
                registered.AssociationId.Value,
                Version: 1,
                registered.OwnerId,
                registered.AssociateId,
                registered.AssociateType,
                registered.ValidityPeriod),
            (null, _) => null,
            (_, PartyAssociationUpdated updated) => view with
            {
                Version = view.Version + 1,
                AssociateType = updated.AssociateType,
                ValidityPeriod = updated.ValidityPeriod,
            },
            (_, _) => view with { Version = view.Version + 1 },
        };
}
