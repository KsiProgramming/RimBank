namespace RimBank.SharedKernel;

/// <summary>
/// The read-side contract of a view (read model): <c>Project(view, event) → view</c> (tech-stack §8.2, ADR 0010).
/// Implemented by a <c>sealed class …Projection</c> in the context's core; Marten only runs it (inline or async,
/// tech-stack §8.3), so the same pure function serves both and can be tested without a database.
/// </summary>
/// <typeparam name="TView">The view, an immutable record shaped for its consumers.</typeparam>
/// <typeparam name="TEvent">The domain event base type the view is projected from.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Project"/> is <c>static</c>, so a projection cannot hold ports, handlers or a
/// <see cref="TimeProvider"/>: any time it shows comes from event data.
/// </para>
/// <para>
/// A view is the fold of its events from <c>null</c>, so it can always be rebuilt from the event store
/// (mission, quality bar "Rebuildable reads").
/// </para>
/// </remarks>
public interface IProjection<TView, TEvent>
    where TView : class
{
    /// <summary>
    /// Applies <paramref name="domainEvent"/> to <paramref name="view"/>. Pure.
    /// </summary>
    /// <param name="view">The current view, or <c>null</c> when there is none yet.</param>
    /// <param name="domainEvent">The next event of the stream (or streams) the view follows.</param>
    /// <returns>
    /// The new view; <c>null</c> when there is no view (not created yet, or removed). An event the view does not
    /// care about returns <paramref name="view"/> unchanged, so new events never break an existing view.
    /// </returns>
    static abstract TView? Project(TView? view, TEvent domainEvent);
}
