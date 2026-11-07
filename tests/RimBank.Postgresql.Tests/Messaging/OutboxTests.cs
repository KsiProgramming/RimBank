using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Postgresql.Tests;

// Roadmap 0.11: a message a handler returns is saved in the same transaction as its events, in the context's own
// schema, goes through the context's durable local queue, and survives a restart (ADR 0017).
public sealed class OutboxTests(PostgresqlServer server)
{
    private const string NotedEnvelopes = "wolverine_incoming_envelopes WHERE message_type LIKE '%FakeAmountNoted'";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Smoke_a_command_is_queried_and_its_message_is_published_and_handled()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();

        (await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation)).IsSuccess.ShouldBeTrue();
        (await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 40m, Keys.New()), Cancellation)).IsSuccess.ShouldBeTrue();
        var view = await host.Bus.InvokeAsync<Result<FakeLedgerView>>(new RetrieveFakeLedger(ledger), Cancellation);
        var events = await RunningHost.WaitForEventAsync<FakeAmountConfirmed>(host.Ledger, ledger);

        view.Value.ShouldBe(new FakeLedgerView(ledger, "Cash", 40m));
        events.ShouldBe([new FakeLedgerOpened(ledger, "Cash"), new FakeAmountPosted(ledger, 40m), new FakeAmountConfirmed(ledger, 40m)]);
    }

    [Fact]
    public async Task The_message_goes_through_the_durable_inbox_in_the_contexts_own_schema()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 40m, Keys.New()), Cancellation);
        await RunningHost.WaitForEventAsync<FakeAmountConfirmed>(host.Ledger, ledger);

        (await CountAsync(host, $"SELECT count(*) FROM fake_ledger.{NotedEnvelopes}")).ShouldBe(1);
        (await CountAsync(host, $"SELECT count(*) FROM fake_registry.{NotedEnvelopes}")).ShouldBe(0);
        (await CountAsync(host, $"SELECT count(*) FROM wolverine.{NotedEnvelopes}")).ShouldBe(0);
    }

    [Fact]
    public async Task A_message_waiting_in_the_contexts_inbox_is_handled_after_a_restart()
    {
        string database;
        var ledger = Guid.CreateVersion7();
        await using (var first = await RunningHost.StartAsync(server))
        {
            database = first.Database;
            await first.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);
            await first.Bus.ScheduleAsync(new FakeAmountNoted(ledger, 7m), TimeSpan.FromSeconds(8));

            (await CountAsync(first, $"SELECT count(*) FROM fake_ledger.{NotedEnvelopes}")).ShouldBe(1);
        }

        // The first host stopped before the message was due, so only the second one can handle it.
        (await RunningHost.CountAsync(server, database, "SELECT count(*) FROM fake_ledger.mt_events WHERE type = 'fake_amount_confirmed'")).ShouldBe(0);

        await using var second = await RunningHost.StartAsync(server, database);
        var events = await RunningHost.WaitForEventAsync<FakeAmountConfirmed>(second.Ledger, ledger);

        events.OfType<FakeAmountConfirmed>().ShouldHaveSingleItem().ShouldBe(new FakeAmountConfirmed(ledger, 7m));
    }

    private Task<long> CountAsync(RunningHost host, string sql) => RunningHost.CountAsync(server, host.Database, sql);
}
