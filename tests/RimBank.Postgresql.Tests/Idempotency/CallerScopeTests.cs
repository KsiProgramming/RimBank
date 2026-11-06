using RimBank.Postgresql.Database.Tests;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Tests;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.Idempotency.Tests;

// Roadmap 0.13: an idempotency key is scoped to the caller who sent it, so two callers can never replay, collide with or
// probe each other's commands (ADR 0018 follow-up, ADR 0019). The caller is the envelope's user name, which the host's
// actor relay sets from the access token; a message no caller sent is scoped to the system actor.
public sealed class CallerScopeTests(PostgresqlServer server)
{
    private const string Alice = "f81d4fae-7dec-11d0-a765-00a0c91e6bf6";
    private const string Bob = "0b8a1c7e-3f52-4e8a-9a51-3c2b7d6e9f10";
    private const string Outcomes = "SELECT count(*) FROM fake_ledger.mt_doc_idempotentoutcome";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_same_key_from_two_callers_is_handled_for_each()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var post = new PostFakeAmount(ledger, 40m, Keys.New());

        var alice = await host.BusOf(Alice).InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);
        var bob = await host.BusOf(Bob).InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);

        alice.IsSuccess.ShouldBeTrue();
        bob.IsSuccess.ShouldBeTrue();
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task The_same_caller_key_and_payload_replays()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var post = new PostFakeAmount(ledger, 40m, Keys.New());

        var first = await host.BusOf(Alice).InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);
        var again = await host.BusOf(Alice).InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);

        again.Value.ShouldBe(first.Value);
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Another_callers_key_with_a_different_payload_is_not_a_reuse()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var key = Keys.New();
        await host.BusOf(Alice).InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 40m, key), Cancellation);

        var bob = await host.BusOf(Bob).InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 41m, key), Cancellation);
        var aliceChanged = await host.BusOf(Alice).InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 42m, key), Cancellation);

        bob.IsSuccess.ShouldBeTrue();
        aliceChanged.Error.ShouldBe(IdempotencyKey.Reused);
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().Select(posted => posted.Amount).ShouldBe([40m, 41m]);
    }

    [Fact]
    public async Task A_message_without_a_caller_is_scoped_to_the_system_actor()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var post = new PostFakeAmount(ledger, 40m, Keys.New());

        var system = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);
        var systemAgain = await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);
        var alice = await host.BusOf(Alice).InvokeAsync<Result<FakeLedgerAcknowledged>>(post, Cancellation);

        systemAgain.Value.ShouldBe(system.Value);
        alice.IsSuccess.ShouldBeTrue();
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Callers_whose_subject_and_key_join_to_the_same_text_do_not_clash()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);

        // "u:x" + "k" and "u" + "x:k" would both read "u:x:k" if the caller and the key were only joined by a colon.
        var first = await host.BusOf("u:x").InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 1m, IdempotencyKey.From("k")), Cancellation);
        var second = await host.BusOf("u").InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledger, 2m, IdempotencyKey.From("x:k")), Cancellation);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().Count().ShouldBe(2);
    }

    // Each caller posts to its own ledger with the same key. The two callers' commands run at the same time by design
    // (their keys do not share a lock), so on one ledger their appends would race for the stream's next version and one
    // would fail with Marten's optimistic concurrency error: a stream conflict, not what this test is about.
    [Fact]
    public async Task Concurrent_calls_with_the_same_key_commit_once_per_caller()
    {
        await using var host = await RunningHost.StartAsync(server);
        var key = Keys.New();
        var ledgers = new Dictionary<string, Guid>
        {
            [Alice] = await OpenLedgerAsync(host),
            [Bob] = await OpenLedgerAsync(host),
        };

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => index % 2 == 0 ? Alice : Bob).Select(caller =>
            Task.Run(
                async () => (Caller: caller, Result: await host.BusOf(caller).InvokeAsync<Result<FakeLedgerAcknowledged>>(new PostFakeAmount(ledgers[caller], 10m, key), Cancellation)),
                Cancellation)));

        results.Where(call => call.Caller == Alice).ShouldContain(call => call.Result.IsSuccess);
        results.Where(call => call.Caller == Bob).ShouldContain(call => call.Result.IsSuccess);
        results.ShouldAllBe(call => call.Result.IsSuccess || call.Result.Error == IdempotencyKey.InUse);
        foreach (var ledger in ledgers.Values)
        {
            (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem();
        }

        (await RunningHost.CountAsync(server, host.Database, Outcomes)).ShouldBe(4); // two openings and one posting per caller
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
}
