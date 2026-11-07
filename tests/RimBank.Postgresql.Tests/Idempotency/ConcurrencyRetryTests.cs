using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.Tests;

// Roadmap 1.8: a command whose commit loses an optimistic-concurrency race runs once more, so its handler's own version
// check answers the conflict as a value instead of an exception reaching the caller (ADR 0027).
public sealed class ConcurrencyRetryTests(PostgresqlServer server)
{
    private const string LedgerOutcomes = "SELECT count(*) FROM fake_ledger.mt_doc_idempotentoutcome";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_command_at_the_current_version_commits_on_the_first_attempt()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);

        var posted = await PostAsync(host, ledger, expectedVersion: 1);

        posted.Value.ShouldBe(new FakeLedgerAcknowledged(ledger));
        PostFakeAmountAtVersionHandler.Attempts[ledger].ShouldBe(1);
    }

    [Fact]
    public async Task A_command_that_loses_the_race_at_commit_is_retried_once_and_returns_the_conflict_as_a_value()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        var interfered = 0;
        PostFakeAmountAtVersionHandler.Interlopers[ledger] = async () =>
        {
            // Another writer commits between the handler's load and its commit, on the first attempt only.
            if (Interlocked.Increment(ref interfered) == 1)
            {
                await AppendAsync(host, ledger, new FakeAmountPosted(ledger, 99m));
            }
        };

        var posted = await PostAsync(host, ledger, expectedVersion: 1);

        posted.Error.ShouldBe(FakeLedgerErrors.VersionConflict);
        PostFakeAmountAtVersionHandler.Attempts[ledger].ShouldBe(2);
        (await StreamAsync(host, ledger)).OfType<FakeAmountPosted>().ShouldHaveSingleItem().Amount.ShouldBe(99m);
        (await RunningHost.CountAsync(server, host.Database, LedgerOutcomes)).ShouldBe(1); // only the opening
    }

    [Fact]
    public async Task The_retry_is_bounded_so_a_second_lost_race_reaches_the_caller()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = await OpenLedgerAsync(host);
        PostFakeAmountAtVersionHandler.Interlopers[ledger] = () => AppendAsync(host, ledger, new FakeAmountPosted(ledger, 1m));

        // With no expected version the handler appends at whatever it loaded, so every attempt passes its own check and
        // loses at commit: one retry, then the exception reaches the caller.
        await Should.ThrowAsync<JasperFx.ConcurrencyException>(() => PostAsync(host, ledger, expectedVersion: null));

        PostFakeAmountAtVersionHandler.Attempts[ledger].ShouldBe(2);
    }

    private static Task<Result<FakeLedgerAcknowledged>> PostAsync(RunningHost host, Guid ledger, long? expectedVersion) =>
        host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(
            new PostFakeAmountAtVersion(ledger, expectedVersion, 10m, Keys.New()), Cancellation);

    private static async Task<Guid> OpenLedgerAsync(RunningHost host)
    {
        var ledger = Guid.CreateVersion7();
        (await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation)).IsSuccess.ShouldBeTrue();
        return ledger;
    }

    private static async Task AppendAsync(RunningHost host, Guid ledger, IFakeLedgerEvent domainEvent)
    {
        await using var session = host.Ledger.LightweightSession();
        session.Events.Append(ledger, domainEvent);
        await session.SaveChangesAsync(Cancellation);
    }

    private static async Task<IReadOnlyList<object>> StreamAsync(RunningHost host, Guid stream)
    {
        await using var query = host.Ledger.QuerySession();
        return [.. (await query.Events.FetchStreamAsync(stream, token: Cancellation)).Select(stored => stored.Data)];
    }
}
