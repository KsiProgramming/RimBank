using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RimBank.Postgresql.FakeContexts.Ledger.Tests;

// The fake ledger's .Postgresql adapter: its store marker, its store options and its port implementations.

/// <summary>The fake ledger's store marker, as a real context declares it in its <c>.Postgresql</c> adapter.</summary>
public interface IFakeLedgerStore : IDocumentStore;

/// <summary>The write port over the session Wolverine opens on the ledger's own store for the handler's transaction.</summary>
public sealed class FakeLedgerJournal(IDocumentSession session) : IFakeLedgerJournal
{
    public async Task<IReadOnlyList<IFakeLedgerEvent>> LoadAsync(Guid ledgerId, CancellationToken cancellationToken)
    {
        var events = await session.Events.FetchStreamAsync(ledgerId, token: cancellationToken);
        return [.. events.Select(stored => stored.Data).OfType<IFakeLedgerEvent>()];
    }

    public void Start(Guid ledgerId, IFakeLedgerEvent domainEvent) => session.Events.StartStream(ledgerId, domainEvent);

    public void Append(Guid ledgerId, IFakeLedgerEvent domainEvent) => session.Events.Append(ledgerId, domainEvent);
}

/// <summary>The read port over the ledger's inline view, with its own query session (no transaction).</summary>
public sealed class FakeLedgerEnquiry(IFakeLedgerStore store) : IFakeLedgerEnquiry
{
    public async Task<FakeLedgerView?> FindAsync(Guid ledgerId, CancellationToken cancellationToken)
    {
        await using var query = store.QuerySession();
        return await query.LoadAsync<FakeLedgerView>(ledgerId, cancellationToken);
    }
}

public static class FakeLedger
{
    public const string ServiceDomain = "FakeLedger";

    /// <summary>What the fake ledger's <c>.Postgresql</c> adapter would pass to <c>AddServiceDomainStore</c>.</summary>
    public static void Configure(StoreOptions options)
    {
        options.Events.MapEventType<FakeLedgerOpened>("fake_ledger_opened");
        options.Events.MapEventType<FakeAmountPosted>("fake_amount_posted");
        options.Events.MapEventType<FakeAmountConfirmed>("fake_amount_confirmed");
        options.Events.MapEventType<FakeLedgerClosed>("fake_ledger_closed");
        options.AddInlineView<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent>();
        options.AddAsyncView<FakeLedgerPostingsView, FakeLedgerPostingsProjection, IFakeLedgerEvent>();
    }

    /// <summary>What the fake ledger's <c>.Postgresql</c> adapter's <c>Add…</c> extension would do.</summary>
    public static IHostApplicationBuilder AddFakeLedger(this IHostApplicationBuilder builder)
    {
        builder.AddServiceDomainStore<IFakeLedgerStore, IFakeLedgerEvent>(ServiceDomain, Configure);
        builder.AddWritePort<IFakeLedgerJournal, FakeLedgerJournal>();
        builder.Services.AddSingleton<IFakeLedgerEnquiry, FakeLedgerEnquiry>();
        return builder;
    }
}
