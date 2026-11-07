using LedgerViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<
    RimBank.SharedKernel.Tests.SampleLedgerViewProjection,
    RimBank.SharedKernel.Tests.SampleLedgerView,
    RimBank.SharedKernel.Tests.SampleLedgerEvent>;

namespace RimBank.SharedKernel.Tests;

// The sample ledger's view, written with the Given events / Then view helper (read side).
public sealed class SampleLedgerViewSpecificationTests
{
    private static readonly Currency _eur = Currency.From("EUR");

    [Fact]
    public void Opened_creates_the_view() =>
        LedgerViewSpecification.Given(new SampleLedgerOpened(_eur))
            .Then(new SampleLedgerView(Money.Zero(_eur), 0, false));

    [Fact]
    public void Postings_add_to_the_balance() =>
        LedgerViewSpecification.Given(
                new SampleLedgerOpened(_eur),
                new SampleLedgerPosted(Money.Of(10.00m, _eur)),
                new SampleLedgerPosted(Money.Of(2.50m, _eur)))
            .Then(new SampleLedgerView(Money.Of(12.50m, _eur), 2, false));

    [Fact]
    public void Closed_marks_the_view_closed() =>
        LedgerViewSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerClosed())
            .Then(new SampleLedgerView(Money.Zero(_eur), 0, true));

    [Fact]
    public void No_events_give_no_view() =>
        LedgerViewSpecification.Given().ThenNoView();

    [Fact]
    public void An_event_before_opened_gives_no_view() =>
        LedgerViewSpecification.Given(new SampleLedgerPosted(Money.Of(1.00m, _eur))).ThenNoView();

    [Fact]
    public void An_event_the_view_ignores_leaves_it_unchanged()
    {
        var view = new SampleLedgerView(Money.Zero(_eur), 0, false);

        SampleLedgerViewProjection.Project(view, new IgnoredEvent()).ShouldBeSameAs(view);
    }

    private sealed record IgnoredEvent : SampleLedgerEvent;
}
