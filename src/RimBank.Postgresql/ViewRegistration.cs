using JasperFx.Events.Projections;
using Marten;
using RimBank.SharedKernel;

namespace RimBank.Postgresql;

/// <summary>
/// Wires a context's views into its store by lifecycle (specs/tech-stack.md §8.3, ADR 0016): inline for a
/// single-stream view that callers read right after writing, async for everything else.
/// </summary>
public static class ViewRegistration
{
    /// <summary>
    /// Projects <typeparamref name="TView"/> in the same transaction as the events, so a caller can read it right
    /// after the command (read-your-write).
    /// </summary>
    /// <typeparam name="TView">The view.</typeparam>
    /// <typeparam name="TProjection">The core's pure projection.</typeparam>
    /// <typeparam name="TEvent">The event type the projection reads.</typeparam>
    /// <param name="options">The context's store options.</param>
    /// <returns>The same options.</returns>
    public static StoreOptions AddInlineView<TView, TProjection, TEvent>(this StoreOptions options)
        where TView : class
        where TProjection : IProjection<TView, TEvent> =>
        Add<TView, TProjection, TEvent>(options, ProjectionLifecycle.Inline);

    /// <summary>
    /// Projects <typeparamref name="TView"/> in the context's own async daemon, after the events are committed. Its
    /// staleness is visible through <see cref="ViewOperations.GetViewProgressAsync"/>.
    /// </summary>
    /// <typeparam name="TView">The view.</typeparam>
    /// <typeparam name="TProjection">The core's pure projection.</typeparam>
    /// <typeparam name="TEvent">The event type the projection reads.</typeparam>
    /// <param name="options">The context's store options.</param>
    /// <returns>The same options.</returns>
    public static StoreOptions AddAsyncView<TView, TProjection, TEvent>(this StoreOptions options)
        where TView : class
        where TProjection : IProjection<TView, TEvent> =>
        Add<TView, TProjection, TEvent>(options, ProjectionLifecycle.Async);

    /// <summary>
    /// The name Marten knows a view's projection by, used for its progress and its rebuild: the core projection's
    /// type name, for example <c>PartyReferenceDataDirectoryEntryViewProjection</c>.
    /// </summary>
    /// <typeparam name="TProjection">The core's pure projection.</typeparam>
    /// <returns>The projection name.</returns>
    public static string ProjectionName<TProjection>() => typeof(TProjection).Name;

    private static StoreOptions Add<TView, TProjection, TEvent>(StoreOptions options, ProjectionLifecycle lifecycle)
        where TView : class
        where TProjection : IProjection<TView, TEvent>
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Projections.Add(new ViewProjector<TView, TProjection, TEvent>(), lifecycle);
        return options;
    }
}
