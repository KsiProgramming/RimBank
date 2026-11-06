using RimBank.Postgresql.Database.Tests;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;
using RimBank.Postgresql.FakeContexts.Tests;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.Messaging.Tests;

// Roadmap 0.11: commands and queries go through IMessageBus.InvokeAsync<Result<T>> to Wolverine-free core handlers,
// and each handler commits through its own context's store (ADR 0017, specs/tech-stack.md §4 and §8.1–8.2).
public sealed class DispatchTests(PostgresqlServer server)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_command_is_dispatched_to_its_context_and_returns_an_acknowledgement()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();

        var acknowledgement = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);

        acknowledgement.IsSuccess.ShouldBeTrue();
        acknowledgement.Value.ShouldBe(new FakeLedgerAcknowledged(ledger));
        await using var query = host.Ledger.QuerySession();
        (await query.Events.FetchStreamAsync(ledger, token: Cancellation)).Select(stored => stored.Data).ShouldBe([new FakeLedgerOpened(ledger, "Cash")]);
    }

    [Fact]
    public async Task A_query_returns_the_inline_view_right_after_the_command()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 25m, Keys.New()), Cancellation);

        var view = await host.Bus.InvokeAsync<Result<FakeLedgerView>>(new RetrieveFakeLedger(ledger), Cancellation);

        view.Value.ShouldBe(new FakeLedgerView(ledger, "Cash", 25m));
    }

    [Fact]
    public async Task A_query_for_an_unknown_record_returns_a_not_found_error_as_a_value()
    {
        await using var host = await RunningHost.StartAsync(server);

        var view = await host.Bus.InvokeAsync<Result<FakeLedgerView>>(new RetrieveFakeLedger(Guid.CreateVersion7()), Cancellation);

        view.IsFailure.ShouldBeTrue();
        view.Error.Kind.ShouldBe(ErrorKind.NotFound);
        view.Error.Code.ShouldBe("fake-ledger.ledger-not-found");
    }

    [Fact]
    public async Task Each_context_handler_commits_through_its_own_store()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        var entry = Guid.CreateVersion7();

        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);
        await host.Bus.InvokeAsync<Result<FakeEntryAcknowledged>>(new RegisterFakeEntry(entry, "Alice", Keys.New()), Cancellation);

        (await CountEventsAsync(host, "fake_ledger", ledger)).ShouldBe(1);
        (await CountEventsAsync(host, "fake_registry", ledger)).ShouldBe(0);
        (await CountEventsAsync(host, "fake_registry", entry)).ShouldBe(1);
        (await CountEventsAsync(host, "fake_ledger", entry)).ShouldBe(0);
    }

    [Fact]
    public async Task A_failed_result_comes_back_as_a_value_and_commits_neither_events_nor_messages()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);

        var rejected = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 0m, Keys.New()), Cancellation);
        var unknown = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(Guid.CreateVersion7(), 10m, Keys.New()), Cancellation);

        rejected.Error.Code.ShouldBe("fake-ledger.amount-not-positive");
        unknown.Error.Kind.ShouldBe(ErrorKind.NotFound);
        (await RunningHost.CountAsync(server, host.Database, "SELECT count(*) FROM fake_ledger.mt_events")).ShouldBe(1);
        (await RunningHost.CountAsync(server, host.Database, "SELECT count(*) FROM fake_ledger.wolverine_incoming_envelopes")).ShouldBe(0);
        (await RunningHost.CountAsync(server, host.Database, "SELECT count(*) FROM fake_ledger.wolverine_outgoing_envelopes")).ShouldBe(0);
    }

    private Task<long> CountEventsAsync(RunningHost host, string schema, Guid stream) =>
        RunningHost.CountAsync(server, host.Database, $"SELECT count(*) FROM {schema}.mt_events WHERE stream_id = '{stream}'");
}
