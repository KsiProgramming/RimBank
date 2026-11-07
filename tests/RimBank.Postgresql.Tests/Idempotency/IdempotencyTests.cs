using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.Tests;

// Roadmap 0.12: every command is handled once per idempotency key, through its own context's store and in the same
// transaction as its events (ADR 0018). The same key and payload give the same result; a different payload gives a
// conflict. Queries carry no key.
public sealed class IdempotencyTests(PostgresqlServer server)
{
    private const string LedgerOutcomes = "SELECT count(*) FROM fake_ledger.mt_doc_idempotentoutcome";
    private const string RegistryOutcomes = "SELECT count(*) FROM fake_registry.mt_doc_idempotentoutcome";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_same_key_and_payload_returns_the_same_acknowledgement_and_appends_once()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var post = new PostFakeAmount(ledger, 40m, Keys.New());

        var first = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);
        var again = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);

        first.Value.ShouldBe(new FakeLedgerAcknowledged(ledger));
        again.IsSuccess.ShouldBeTrue();
        again.Value.ShouldBe(first.Value);
        var events = await RunningHost.WaitForEventAsync<FakeAmountConfirmed>(host.Ledger, ledger);
        events.OfType<FakeAmountPosted>().ShouldHaveSingleItem();
        (await CountAsync(host, "SELECT count(*) FROM fake_ledger.wolverine_incoming_envelopes WHERE message_type LIKE '%FakeAmountNoted'")).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_with_a_different_payload_returns_an_idempotency_conflict()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var key = Keys.New();
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 40m, key), Cancellation);

        var changed = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 41m, key), Cancellation);

        changed.IsFailure.ShouldBeTrue();
        changed.Error.ShouldBe(IdempotencyKey.Reused);
        changed.Error.Kind.ShouldBe(ErrorKind.Conflict);
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem().Amount.ShouldBe(40m);
    }

    [Fact]
    public async Task Concurrent_calls_with_the_same_key_commit_once()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var post = new PostFakeAmount(ledger, 10m, Keys.New());

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation), Cancellation)));

        results.ShouldContain(result => result.IsSuccess);
        results.ShouldAllBe(result => result.IsSuccess
            ? result.Value == new FakeLedgerAcknowledged(ledger)
            : result.Error == IdempotencyKey.InUse);
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem();
        (await CountAsync(host, LedgerOutcomes)).ShouldBe(2); // the opening and the one posting
    }

    [Fact]
    public async Task A_failed_result_stores_no_outcome_and_can_be_retried_with_the_same_key()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        var post = new PostFakeAmount(ledger, 15m, Keys.New());

        var rejected = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);
        (await CountAsync(host, LedgerOutcomes)).ShouldBe(0);
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);
        var retried = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);

        rejected.Error.Code.ShouldBe("fake-ledger.ledger-not-found");
        retried.Value.ShouldBe(new FakeLedgerAcknowledged(ledger));
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_same_key_in_two_contexts_does_not_clash()
    {
        await using var host = await RunningHost.StartAsync(server);
        var key = Keys.New();

        var ledger = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(Guid.CreateVersion7(), "Cash", key), Cancellation);
        var entry = await host.Bus.InvokeAsync<Result<FakeEntryAcknowledged>>(new RegisterFakeEntry(Guid.CreateVersion7(), "Alice", key), Cancellation);

        ledger.IsSuccess.ShouldBeTrue();
        entry.IsSuccess.ShouldBeTrue();
        (await CountAsync(host, LedgerOutcomes)).ShouldBe(1);
        (await CountAsync(host, RegistryOutcomes)).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_on_two_command_types_does_not_clash()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        var key = Keys.New();

        var opened = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", key), Cancellation);
        var posted = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 5m, key), Cancellation);

        opened.IsSuccess.ShouldBeTrue();
        posted.IsSuccess.ShouldBeTrue();
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_command_without_a_key_is_refused_and_commits_nothing()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();

        var refused = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", default), Cancellation);

        refused.Error.ShouldBe(IdempotencyKey.Missing);
        (await StreamAsync(host, ledger)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Queries_are_not_wrapped_by_the_idempotency_policy()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);

        var first = await host.Bus.InvokeAsync<Result<FakeLedgerView>>(new RetrieveFakeLedger(ledger), Cancellation);
        var again = await host.Bus.InvokeAsync<Result<FakeLedgerView>>(new RetrieveFakeLedger(ledger), Cancellation);

        first.Value.ShouldBe(new FakeLedgerView(ledger, "Cash", 0m));
        again.Value.ShouldBe(first.Value);
        (await CountAsync(host, LedgerOutcomes)).ShouldBe(1); // only the opening command
    }

    [Fact]
    public async Task Only_the_fingerprint_is_stored_never_the_payload()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Very private name", Keys.New()), Cancellation);

        (await CountAsync(host, "SELECT count(*) FROM fake_ledger.mt_doc_idempotentoutcome WHERE data::text LIKE '%Very private name%'")).ShouldBe(0);
        (await CountAsync(host, "SELECT count(*) FROM fake_ledger.mt_doc_idempotentoutcome WHERE length(data ->> 'Fingerprint') = 64")).ShouldBe(1);
    }

    [Fact]
    public void The_fingerprint_ignores_the_key_and_changes_with_the_payload()
    {
        var ledger = Guid.CreateVersion7();

        IdempotencyGate.Fingerprint(new PostFakeAmount(ledger, 1m, Keys.New()))
            .ShouldBe(IdempotencyGate.Fingerprint(new PostFakeAmount(ledger, 1m, Keys.New())));
        IdempotencyGate.Fingerprint(new PostFakeAmount(ledger, 1m, Keys.New()))
            .ShouldNotBe(IdempotencyGate.Fingerprint(new PostFakeAmount(ledger, 1.01m, Keys.New())));
        IdempotencyGate.Fingerprint(new PostFakeAmount(ledger, 1m, Keys.New()))
            .ShouldNotBe(IdempotencyGate.Fingerprint(new PostFakeAmount(Guid.CreateVersion7(), 1m, Keys.New())));
    }

    private static async Task<Guid> OpenLedgerAsync(RunningHost host)
    {
        var ledger = Guid.CreateVersion7();
        (await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation)).IsSuccess.ShouldBeTrue();
        return ledger;
    }

    private static async Task<IReadOnlyList<object>> StreamAsync(RunningHost host, Guid stream)
    {
        await using var query = host.Ledger.QuerySession();
        return [.. (await query.Events.FetchStreamAsync(stream, token: Cancellation)).Select(stored => stored.Data)];
    }

    private Task<long> CountAsync(RunningHost host, string sql) => RunningHost.CountAsync(server, host.Database, sql);
}
