namespace RimBank.SharedKernel.DecidersAndProjections.Tests;

// Project called directly, without the specification helpers.
public sealed class SampleLedgerViewProjectionTests
{
    private static readonly Currency _eur = Currency.From("EUR");

    private static SampleLedgerView? Fold(params SampleLedgerEvent[] events) =>
        events.Aggregate((SampleLedgerView?)null, SampleLedgerViewProjection.Project);

    [Fact]
    public void Opened_creates_the_view() =>
        Fold(new SampleLedgerOpened(_eur)).ShouldBe(new SampleLedgerView(Money.Zero(_eur), 0, false));

    [Fact]
    public void Posted_adds_to_the_balance() =>
        Fold(new SampleLedgerOpened(_eur), new SampleLedgerPosted(Money.Of(10.00m, _eur)), new SampleLedgerPosted(Money.Of(2.50m, _eur)))
            .ShouldBe(new SampleLedgerView(Money.Of(12.50m, _eur), 2, false));

    [Fact]
    public void Closed_marks_the_view_closed() =>
        Fold(new SampleLedgerOpened(_eur), new SampleLedgerClosed()).ShouldNotBeNull().IsClosed.ShouldBeTrue();

    [Fact]
    public void An_event_before_opened_leaves_no_view() =>
        Fold(new SampleLedgerPosted(Money.Of(1m, _eur))).ShouldBeNull();

    [Fact]
    public void An_event_the_view_ignores_leaves_it_unchanged()
    {
        var view = Fold(new SampleLedgerOpened(_eur));

        SampleLedgerViewProjection.Project(view, new IgnoredEvent()).ShouldBeSameAs(view);
    }

    private sealed record IgnoredEvent : SampleLedgerEvent;
}
