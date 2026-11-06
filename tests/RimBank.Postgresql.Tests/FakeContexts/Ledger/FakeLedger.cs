using RimBank.SharedKernel;

namespace RimBank.Postgresql.FakeContexts.Ledger.Tests;

// A test-only context that proves the conventions without a real service domain ("no speculative coverage").
// This file is its core: events, views, pure projections, commands, queries, handlers and ports. Like a real core
// (specs/tech-stack.md §4), it references neither Marten nor Wolverine; FakeLedgerAdapter.cs plays its .Postgresql.

/// <summary>Every fake ledger event.</summary>
public interface IFakeLedgerEvent;

public sealed record FakeLedgerOpened(Guid LedgerId, string Name) : IFakeLedgerEvent;

public sealed record FakeAmountPosted(Guid LedgerId, decimal Amount) : IFakeLedgerEvent;

/// <summary>Recorded by the handler of <see cref="FakeAmountNoted"/>, so a test can see that the message was handled.</summary>
public sealed record FakeAmountConfirmed(Guid LedgerId, decimal Amount) : IFakeLedgerEvent;

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

/// <summary>Write port: the ledger's event stream, inside the handler's transaction.</summary>
public interface IFakeLedgerJournal : IWritePort
{
    Task<IReadOnlyList<IFakeLedgerEvent>> LoadAsync(Guid ledgerId, CancellationToken cancellationToken);

    void Start(Guid ledgerId, IFakeLedgerEvent domainEvent);

    void Append(Guid ledgerId, IFakeLedgerEvent domainEvent);
}

/// <summary>Read port: the ledger's views.</summary>
public interface IFakeLedgerEnquiry : IReadPort
{
    Task<FakeLedgerView?> FindAsync(Guid ledgerId, CancellationToken cancellationToken);
}

public sealed record OpenFakeLedger(Guid LedgerId, string Name, IdempotencyKey IdempotencyKey) : ICommand;

public sealed record PostFakeAmount(Guid LedgerId, decimal Amount, IdempotencyKey IdempotencyKey) : ICommand;

/// <summary>The acknowledgement of a command: what was decided, never a read model (specs/tech-stack.md §8.1).</summary>
public sealed record FakeLedgerAcknowledged(Guid LedgerId);

/// <summary>A message the ledger publishes to its own durable local queue after a posting is committed.</summary>
public sealed record FakeAmountNoted(Guid LedgerId, decimal Amount);

public sealed record RetrieveFakeLedger(Guid LedgerId) : IQuery<FakeLedgerView>;

public static class FakeLedgerErrors
{
    public static Error LedgerNotFound(Guid ledgerId) => Error.NotFound("fake-ledger.ledger-not-found", $"Fake ledger {ledgerId} does not exist.");

    public static Error AmountNotPositive => Error.Validation("fake-ledger.amount-not-positive", "The amount must be positive.");
}

public static class OpenFakeLedgerHandler
{
    public static Result<FakeLedgerAcknowledged> Handle(OpenFakeLedger command, IFakeLedgerJournal journal)
    {
        journal.Start(command.LedgerId, new FakeLedgerOpened(command.LedgerId, command.Name));
        return new FakeLedgerAcknowledged(command.LedgerId);
    }
}

/// <summary>
/// Decides, appends only when the decision succeeds, and returns the acknowledgement with the message to cascade.
/// A failed decision appends nothing and cascades nothing, so the transaction has nothing to commit.
/// </summary>
public static class PostFakeAmountHandler
{
    public static async Task<(Result<FakeLedgerAcknowledged>, FakeAmountNoted?)> Handle(
        PostFakeAmount command,
        IFakeLedgerJournal journal,
        CancellationToken cancellationToken)
    {
        if (command.Amount <= 0m)
        {
            return (FakeLedgerErrors.AmountNotPositive, null);
        }

        var history = await journal.LoadAsync(command.LedgerId, cancellationToken);
        if (history.Count == 0 || history[^1] is FakeLedgerClosed)
        {
            return (FakeLedgerErrors.LedgerNotFound(command.LedgerId), null);
        }

        journal.Append(command.LedgerId, new FakeAmountPosted(command.LedgerId, command.Amount));
        return (new FakeLedgerAcknowledged(command.LedgerId), new FakeAmountNoted(command.LedgerId, command.Amount));
    }
}

/// <summary>Handles the ledger's own message from its durable local queue, through its own store.</summary>
public static class FakeAmountNotedHandler
{
    public static void Handle(FakeAmountNoted message, IFakeLedgerJournal journal) =>
        journal.Append(message.LedgerId, new FakeAmountConfirmed(message.LedgerId, message.Amount));
}

public static class RetrieveFakeLedgerHandler
{
    public static async Task<Result<FakeLedgerView>> Handle(RetrieveFakeLedger query, IFakeLedgerEnquiry enquiry, CancellationToken cancellationToken) =>
        await enquiry.FindAsync(query.LedgerId, cancellationToken) is { } view
            ? view
            : FakeLedgerErrors.LedgerNotFound(query.LedgerId);
}
