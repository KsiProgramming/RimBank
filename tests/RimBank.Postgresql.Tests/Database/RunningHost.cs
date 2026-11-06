using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RimBank.Postgresql.FakeContexts.Tests;

namespace RimBank.Postgresql.Database.Tests;

/// <summary>A started host with both fake contexts on its own database; disposing it stops the daemons first.</summary>
public sealed class RunningHost(IHost host, string database) : IAsyncDisposable
{
    public string Database { get; } = database;

    public IServiceProvider Services => host.Services;

    public IFakeLedgerStore Ledger => Services.GetRequiredService<IFakeLedgerStore>();

    public IFakeRegistryStore Registry => Services.GetRequiredService<IFakeRegistryStore>();

    public static async Task<RunningHost> StartAsync(PostgresqlServer server)
    {
        var database = await server.CreateDatabaseAsync();
        var host = server.BuildHost(database);
        await PostgresqlServer.StartAsync(host);
        return new RunningHost(host, database);
    }

    /// <summary>Appends events to a new stream in <paramref name="store"/> and returns the sequence of the last one.</summary>
    public static async Task<long> StartStreamAsync(IDocumentStore store, Guid stream, params object[] events)
    {
        await using var session = store.LightweightSession();
        session.Events.StartStream(stream, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        return await LastSequenceAsync(store, stream);
    }

    /// <summary>Appends events to an existing stream and returns the sequence of the last one.</summary>
    public static async Task<long> AppendAsync(IDocumentStore store, Guid stream, params object[] events)
    {
        await using var session = store.LightweightSession();
        session.Events.Append(stream, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        return await LastSequenceAsync(store, stream);
    }

    /// <summary>Waits until the async view has applied <paramref name="sequence"/>, through the progress API.</summary>
    public static async Task<ProjectionProgress> WaitForViewAsync(IDocumentStore store, string projectionName, long sequence)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            var progress = await store.GetViewProgressAsync(projectionName, timeout.Token);
            if (progress.ProcessedSequence >= sequence)
            {
                return progress;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(CancellationToken.None);
        host.Dispose();
    }

    private static async Task<long> LastSequenceAsync(IDocumentStore store, Guid stream)
    {
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(stream, token: TestContext.Current.CancellationToken);
        return events[^1].Sequence;
    }
}
