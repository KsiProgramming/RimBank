using Npgsql;
using RimBank.Postgresql.FakeContexts.Tests;

namespace RimBank.Postgresql.Database.Tests;

// Roadmap 0.10 smoke tests against a real PostgreSQL: append, inline read-your-write, async catch-up and rebuild,
// each on two fake contexts that share a server but nothing else (ADR 0016).
public sealed class EventStoreSmokeTests(PostgresqlServer server)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Appending_events_and_loading_the_stream_returns_them_in_order()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        var opened = new FakeLedgerOpened(ledger, "Cash");
        var posted = new FakeAmountPosted(ledger, 12.34m);

        await RunningHost.StartStreamAsync(host.Ledger, ledger, opened, posted);

        await using var query = host.Ledger.QuerySession();
        var events = await query.Events.FetchStreamAsync(ledger, token: Cancellation);
        events.Select(stored => stored.Data).ShouldBe([opened, posted]);
        events.Select(stored => stored.Version).ShouldBe([1L, 2L]);
        events.Select(stored => stored.EventTypeName).ShouldBe(["fake_ledger_opened", "fake_amount_posted"]);
        (await CountAsync(host.Database, $"SELECT count(*) FROM fake_ledger.mt_events WHERE stream_id = '{ledger}'")).ShouldBe(2);
    }

    [Fact]
    public async Task An_inline_view_is_readable_right_after_saving()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();

        await RunningHost.StartStreamAsync(host.Ledger, ledger, new FakeLedgerOpened(ledger, "Cash"), new FakeAmountPosted(ledger, 100m));

        await using var query = host.Ledger.QuerySession();
        (await query.LoadAsync<FakeLedgerView>(ledger, Cancellation)).ShouldBe(new FakeLedgerView(ledger, "Cash", 100m));
    }

    [Fact]
    public async Task A_null_view_deletes_the_inline_document()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        await RunningHost.StartStreamAsync(host.Ledger, ledger, new FakeLedgerOpened(ledger, "Cash"));

        await RunningHost.AppendAsync(host.Ledger, ledger, new FakeLedgerClosed(ledger));

        await using var query = host.Ledger.QuerySession();
        (await query.LoadAsync<FakeLedgerView>(ledger, Cancellation)).ShouldBeNull();
    }

    [Fact]
    public async Task The_async_daemon_catches_up_and_reports_its_progress()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        var last = await RunningHost.StartStreamAsync(
            host.Ledger, ledger, new FakeLedgerOpened(ledger, "Cash"), new FakeAmountPosted(ledger, 1m), new FakeAmountPosted(ledger, 2m));

        var progress = await RunningHost.WaitForViewAsync(host.Ledger, ViewRegistration.ProjectionName<FakeLedgerPostingsProjection>(), last);

        progress.ProjectionName.ShouldBe("FakeLedgerPostingsProjection");
        progress.ProcessedSequence.ShouldBeGreaterThanOrEqualTo(last);
        await using var query = host.Ledger.QuerySession();
        (await query.LoadAsync<FakeLedgerPostingsView>(ledger, Cancellation)).ShouldBe(new FakeLedgerPostingsView(ledger, 2));
    }

    [Fact]
    public async Task Rebuilding_a_view_yields_the_same_view_and_leaves_the_other_context_alone()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        var entry = Guid.CreateVersion7();
        await RunningHost.StartStreamAsync(host.Ledger, ledger, new FakeLedgerOpened(ledger, "Cash"), new FakeAmountPosted(ledger, 7m));
        await RunningHost.StartStreamAsync(host.Registry, entry, new FakeEntryRegistered(entry, "Alice"));
        await using (var session = host.Ledger.LightweightSession())
        {
            session.HardDeleteWhere<FakeLedgerView>(view => view.Id == ledger);
            await session.SaveChangesAsync(Cancellation);
        }

        await using (var deleted = host.Ledger.QuerySession())
        {
            (await deleted.LoadAsync<FakeLedgerView>(ledger, Cancellation)).ShouldBeNull();
        }

        await host.Ledger.RebuildViewAsync(ViewRegistration.ProjectionName<FakeLedgerViewProjection>(), TimeSpan.FromSeconds(60), Cancellation);

        await using var ledgerQuery = host.Ledger.QuerySession();
        (await ledgerQuery.LoadAsync<FakeLedgerView>(ledger, Cancellation)).ShouldBe(new FakeLedgerView(ledger, "Cash", 7m));
        await using var registryQuery = host.Registry.QuerySession();
        (await registryQuery.LoadAsync<FakeRegistryEntryView>(entry, Cancellation)).ShouldBe(new FakeRegistryEntryView(entry, "Alice"));
    }

    private async Task<long> CountAsync(string database, string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(server.AdministratorConnectionString(database));
        await using var command = dataSource.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync(Cancellation))!;
    }
}
