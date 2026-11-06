using JasperFx.Events;
using Marten.Events.Aggregation;
using RimBank.SharedKernel;

namespace RimBank.Postgresql;

/// <summary>
/// Runs a context's pure <see cref="IProjection{TView, TEvent}"/> as a Marten single-stream projection (ADR 0010,
/// ADR 0016). The view's identity is its stream's id. The adapter only calls
/// <see cref="IProjection{TView, TEvent}.Project"/>: a <c>null</c> view deletes the document, and an event that is not a
/// <typeparamref name="TEvent"/> leaves the view as it is. All the logic stays in the core, where it is unit tested.
/// </summary>
/// <typeparam name="TView">The view, an immutable record with a <c>Guid Id</c> equal to its stream's id.</typeparam>
/// <typeparam name="TProjection">The core's pure projection.</typeparam>
/// <typeparam name="TEvent">The event type the projection reads, usually the context's event interface.</typeparam>
public sealed class ViewProjector<TView, TProjection, TEvent> : SingleStreamProjection<TView, Guid>
    where TView : class
    where TProjection : IProjection<TView, TEvent>
{
    /// <summary>Creates the adapter, named after <typeparamref name="TProjection"/>.</summary>
    public ViewProjector()
    {
        Name = ViewRegistration.ProjectionName<TProjection>();
    }

    /// <inheritdoc />
    public override TView? Evolve(TView? snapshot, Guid id, IEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e.Data is TEvent domainEvent ? TProjection.Project(snapshot, domainEvent) : snapshot;
    }
}
