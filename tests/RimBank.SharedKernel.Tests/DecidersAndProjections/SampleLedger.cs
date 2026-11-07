using System.Diagnostics;

namespace RimBank.SharedKernel.Tests;

// A tiny aggregate that exists only to exercise IDecider, IProjection and the test helpers (ADR 0010),
// like SampleId for typed identifiers. Real aggregates live in a context's core, for example
// PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntry.cs.

public abstract record SampleLedgerCommand;

public sealed record OpenSampleLedger(Currency Currency) : SampleLedgerCommand;

public sealed record PostToSampleLedger(Money Amount) : SampleLedgerCommand;

public sealed record CloseSampleLedger : SampleLedgerCommand;

public abstract record SampleLedgerEvent;

public sealed record SampleLedgerOpened(Currency Currency) : SampleLedgerEvent;

public sealed record SampleLedgerPosted(Money Amount) : SampleLedgerEvent;

public sealed record SampleLedgerClosed : SampleLedgerEvent;

public enum SampleLedgerStatus
{
    NotOpened,
    Open,
    Closed,
}

// State + evolve. The decisions are in SampleLedger.Decisions.cs.
public sealed partial record SampleLedger(SampleLedgerStatus Status, Money? Balance, int Postings)
    : IDecider<SampleLedger, SampleLedgerCommand, SampleLedgerEvent>
{
    public static SampleLedger Initial { get; } = new(SampleLedgerStatus.NotOpened, null, 0);

    public static SampleLedger Evolve(SampleLedger state, SampleLedgerEvent domainEvent) => domainEvent switch
    {
        SampleLedgerOpened opened => state with { Status = SampleLedgerStatus.Open, Balance = Money.Zero(opened.Currency) },
        SampleLedgerPosted posted => state with { Balance = state.Balance!.Value + posted.Amount, Postings = state.Postings + 1 },
        SampleLedgerClosed => state with { Status = SampleLedgerStatus.Closed },
        _ => throw new UnreachableException($"SampleLedger does not know the event {domainEvent}."),
    };
}
