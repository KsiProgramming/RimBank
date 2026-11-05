namespace RimBank.SharedKernel.DecidersAndProjections.Tests;

// Pure projection: (view, event) → view. Opened creates the view; an event before it leaves no view.
public sealed class SampleLedgerViewProjection : IProjection<SampleLedgerView, SampleLedgerEvent>
{
    private SampleLedgerViewProjection()
    {
    }

    public static SampleLedgerView? Project(SampleLedgerView? view, SampleLedgerEvent domainEvent) => (view, domainEvent) switch
    {
        (null, SampleLedgerOpened opened) => new SampleLedgerView(Money.Zero(opened.Currency), 0, false),
        (null, _) => null,
        (_, SampleLedgerPosted posted) => view with { Balance = view.Balance + posted.Amount, Postings = view.Postings + 1 },
        (_, SampleLedgerClosed) => view with { IsClosed = true },
        _ => view,
    };
}
