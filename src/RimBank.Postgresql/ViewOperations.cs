using JasperFx.Events.Projections;
using Marten;

namespace RimBank.Postgresql;

/// <summary>How far an async view has caught up with its context's event store (specs/tech-stack.md §8.3).</summary>
/// <param name="ProjectionName">The projection's name, see <see cref="ViewRegistration.ProjectionName{TProjection}"/>.</param>
/// <param name="ProcessedSequence">The last event sequence the projection has applied.</param>
/// <param name="HighWaterMark">The highest event sequence the context's daemon knows is safe to process.</param>
public sealed record ProjectionProgress(string ProjectionName, long ProcessedSequence, long HighWaterMark)
{
    /// <summary>How many event sequences the view is behind; zero when it has caught up.</summary>
    public long Lag => Math.Max(0, HighWaterMark - ProcessedSequence);

    /// <summary>The view has applied every event up to the high water mark.</summary>
    public bool IsCaughtUp => ProcessedSequence >= HighWaterMark;
}

/// <summary>Operations on a context's views: their progress, and rebuilding them from the event store (ADR 0016).</summary>
public static class ViewOperations
{
    /// <summary>Reads how far an async view has caught up. A projection that never ran reports sequence 0.</summary>
    /// <param name="store">The context's store.</param>
    /// <param name="projectionName">The projection's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The view's progress.</returns>
    public static async Task<ProjectionProgress> GetViewProgressAsync(this IDocumentStore store, string projectionName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var shards = await store.Advanced.AllProjectionProgress(token: cancellationToken).ConfigureAwait(false);
        var processed = shards.Where(shard => shard.ShardName.StartsWith(projectionName + ":", StringComparison.Ordinal))
            .Select(shard => shard.Sequence)
            .DefaultIfEmpty(0)
            .Min();
        var highWaterMark = await store.Advanced.ProjectionProgressFor(new ShardName(ShardState.HighWaterMark), token: cancellationToken).ConfigureAwait(false);
        return new ProjectionProgress(projectionName, processed, highWaterMark);
    }

    /// <summary>
    /// Rebuilds a view from the context's event store: its documents are deleted and every event is projected again.
    /// The other contexts' stores and daemons are not touched.
    /// </summary>
    /// <param name="store">The context's store.</param>
    /// <param name="projectionName">The projection's name.</param>
    /// <param name="timeout">How long the rebuild may take before it is cancelled.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <returns>A task that completes when the view is rebuilt.</returns>
    public static async Task RebuildViewAsync(this IDocumentStore store, string projectionName, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var daemon = await store.BuildProjectionDaemonAsync().ConfigureAwait(false);
        await daemon.RebuildProjectionAsync(projectionName, deadline.Token).ConfigureAwait(false);
    }
}
