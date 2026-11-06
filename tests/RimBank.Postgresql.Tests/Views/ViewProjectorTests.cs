using JasperFx.Events;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;
using RimBank.SharedKernel.Testing;

namespace RimBank.Postgresql.Views.Tests;

// The Marten adapter must add nothing to the core's pure projection (ADR 0016): for the same events it gives the same
// view as the ProjectionSpecification Given/Then helper, and a null view deletes the document.
public sealed class ViewProjectorTests
{
    private static readonly Guid _ledger = Guid.CreateVersion7();

    [Fact]
    public void The_adapter_gives_the_same_view_as_the_pure_projection()
    {
        IFakeLedgerEvent[] events = [new FakeLedgerOpened(_ledger, "Cash"), new FakeAmountPosted(_ledger, 10.50m), new FakeAmountPosted(_ledger, -0.25m)];
        var expected = new FakeLedgerView(_ledger, "Cash", 10.25m);

        ProjectionSpecification<FakeLedgerViewProjection, FakeLedgerView, IFakeLedgerEvent>.Given(events).Then(expected);
        Apply(new ViewProjector<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent>(), events).ShouldBe(expected);
    }

    [Fact]
    public void A_null_view_from_the_pure_projection_deletes_the_document()
    {
        IFakeLedgerEvent[] events = [new FakeLedgerOpened(_ledger, "Cash"), new FakeLedgerClosed(_ledger)];

        ProjectionSpecification<FakeLedgerViewProjection, FakeLedgerView, IFakeLedgerEvent>.Given(events).ThenNoView();
        Apply(new ViewProjector<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent>(), events).ShouldBeNull();
    }

    [Fact]
    public void An_event_the_projection_does_not_read_leaves_the_view_as_it_is()
    {
        var projection = new ViewProjector<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent>();
        var view = new FakeLedgerView(_ledger, "Cash", 5m);

        projection.Evolve(view, _ledger, new Event<FakeEntryRegistered>(new FakeEntryRegistered(_ledger, "Other"))).ShouldBeSameAs(view);
    }

    [Fact]
    public void The_projection_is_named_after_the_core_projection()
    {
        new ViewProjector<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent>().Name.ShouldBe("FakeLedgerViewProjection");
        ViewRegistration.ProjectionName<FakeLedgerPostingsProjection>().ShouldBe("FakeLedgerPostingsProjection");
    }

    [Theory]
    [InlineData(10, 10, 0, true)]
    [InlineData(7, 10, 3, false)]
    [InlineData(12, 10, 0, true)]
    public void Progress_reports_the_lag_behind_the_high_water_mark(long processed, long highWaterMark, long lag, bool caughtUp)
    {
        var progress = new ProjectionProgress("FakeLedgerPostingsProjection", processed, highWaterMark);

        progress.Lag.ShouldBe(lag);
        progress.IsCaughtUp.ShouldBe(caughtUp);
    }

    private static FakeLedgerView? Apply(ViewProjector<FakeLedgerView, FakeLedgerViewProjection, IFakeLedgerEvent> projection, IEnumerable<IFakeLedgerEvent> events) =>
        events.Aggregate((FakeLedgerView?)null, (view, domainEvent) => projection.Evolve(view, _ledger, Wrap(domainEvent)));

    private static IEvent Wrap(IFakeLedgerEvent domainEvent) => domainEvent switch
    {
        FakeLedgerOpened opened => new Event<FakeLedgerOpened>(opened),
        FakeAmountPosted posted => new Event<FakeAmountPosted>(posted),
        FakeLedgerClosed closed => new Event<FakeLedgerClosed>(closed),
        _ => throw new ArgumentOutOfRangeException(nameof(domainEvent), domainEvent, "Not a fake ledger event."),
    };
}
