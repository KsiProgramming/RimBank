using Marten;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.FakeContexts.Tests;

// A test-only context that proves the conventions without a real service domain ("no speculative coverage"):
// its store, its events, an inline view (read-your-write) and an async view (run by its own daemon).

/// <summary>The fake ledger's store marker, as a real context declares it in its <c>.Postgresql</c> adapter.</summary>
public interface IFakeLedgerStore : IDocumentStore;

/// <summary>Every fake ledger event.</summary>
public interface IFakeLedgerEvent;

public sealed record FakeLedgerOpened(Guid LedgerId, string Name) : IFakeLedgerEvent;

public sealed record FakeAmountPosted(Guid LedgerId, decimal Amount) : IFakeLedgerEvent;

public sealed record FakeLedgerClosed(Guid LedgerId) : IFakeLedgerEvent;

/// <summary>Inline view: the ledger as a caller reads it right after a command.</summary>
public sealed record FakeLedgerView(Guid Id, string Name, decimal Balance);

/// <summary>Async view: how many amounts were posted.</summary>
public sealed record FakeLedgerPostingsView(Guid Id, int Postings);

public sealed class FakeLedgerViewProjection : IProjection<FakeLedgerView, IFakeLedgerEvent>
{
    public static FakeLedgerView? Project(FakeLedgerView? view, IFakeLedgerEvent domainEvent) => domainEvent switch
    {
        FakeLedgerOpened opened => new FakeLedgerView(opened.LedgerId, opened.Name, 0m),
        FakeAmountPosted posted when view is not null => view with { Balance = view.Balance + posted.Amount },
        FakeLedgerClosed => null,
        _ => view,
    };
}

public sealed class FakeLedgerPostingsProjection : IProjection<FakeLedgerPostingsView, IFakeLedgerEvent>
{
    public static FakeLedgerPostingsView? Project(FakeLedgerPostingsView? view, IFakeLedgerEvent domainEvent) => domainEvent switch
    {
        FakeLedgerOpened opened => new FakeLedgerPostingsView(opened.LedgerId, 0),
        FakeAmountPosted when view is not null => view with { Postings = view.Postings + 1 },
        _ => view,
    };
}

public static class FakeLedger
{
    public const string ServiceDomain = "FakeLedger";

    /// <summary>What the fake ledger's <c>.Postgresql</c> adapter would pass to <c>AddServiceDomainStore</c>.</summary>
    public static void Configure(StoreOptions options)
    {
        options.Events.MapEventType<FakeLedgerOpened>("fake_ledger_opened");
        options.Events.MapEventType<FakeAmountPosted>("fake_amount_posted");
        options.Events.MapEventType<FakeLedgerClosed>("fake_ledger_closed");
        options.AddInlineView<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent>();
        options.AddAsyncView<FakeLedgerPostingsView, FakeLedgerPostingsProjection, IFakeLedgerEvent>();
    }
}
