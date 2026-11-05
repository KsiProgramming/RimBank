using System.Diagnostics;

namespace RimBank.SharedKernel.DecidersAndProjections.Tests;

// Decide and Evolve called directly, without the specification helpers.
public sealed class SampleLedgerDecisionTests
{
    private static readonly Currency _eur = Currency.From("EUR");
    private static readonly Currency _usd = Currency.From("USD");

    private static SampleLedger Fold(params SampleLedgerEvent[] events) =>
        events.Aggregate(SampleLedger.Initial, SampleLedger.Evolve);

    [Fact]
    public void Initial_is_not_opened()
    {
        SampleLedger.Initial.Status.ShouldBe(SampleLedgerStatus.NotOpened);
        SampleLedger.Initial.Balance.ShouldBeNull();
    }

    [Fact]
    public void Opening_a_new_ledger_decides_opened()
    {
        var result = SampleLedger.Decide(new OpenSampleLedger(_eur), SampleLedger.Initial);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([new SampleLedgerOpened(_eur)]);
    }

    [Fact]
    public void Opening_twice_is_a_conflict()
    {
        var result = SampleLedger.Decide(new OpenSampleLedger(_eur), Fold(new SampleLedgerOpened(_eur)));

        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        result.Error.Code.ShouldBe(SampleLedger.AlreadyOpenedCode);
    }

    [Fact]
    public void Posting_to_an_open_ledger_decides_posted()
    {
        var amount = Money.Of(10.00m, _eur);

        var result = SampleLedger.Decide(new PostToSampleLedger(amount), Fold(new SampleLedgerOpened(_eur)));

        result.Value.ShouldBe([new SampleLedgerPosted(amount)]);
    }

    [Fact]
    public void Posting_before_opening_is_a_conflict() =>
        SampleLedger.Decide(new PostToSampleLedger(Money.Of(1m, _eur)), SampleLedger.Initial)
            .Error.Code.ShouldBe(SampleLedger.NotOpenedCode);

    [Fact]
    public void Posting_to_a_closed_ledger_is_a_conflict() =>
        SampleLedger.Decide(new PostToSampleLedger(Money.Of(1m, _eur)), Fold(new SampleLedgerOpened(_eur), new SampleLedgerClosed()))
            .Error.Code.ShouldBe(SampleLedger.ClosedCode);

    [Fact]
    public void Posting_another_currency_is_a_validation_error()
    {
        var result = SampleLedger.Decide(new PostToSampleLedger(Money.Of(1m, _usd)), Fold(new SampleLedgerOpened(_eur)));

        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe(Money.CurrencyMismatchCode);
    }

    [Fact]
    public void Closing_twice_is_an_accepted_no_op() =>
        SampleLedger.Decide(new CloseSampleLedger(), Fold(new SampleLedgerOpened(_eur), new SampleLedgerClosed()))
            .Value.ShouldBeEmpty();

    [Fact]
    public void Evolve_folds_postings_into_the_balance()
    {
        var state = Fold(
            new SampleLedgerOpened(_eur),
            new SampleLedgerPosted(Money.Of(10.00m, _eur)),
            new SampleLedgerPosted(Money.Of(-2.50m, _eur)));

        state.ShouldBe(new SampleLedger(SampleLedgerStatus.Open, Money.Of(7.50m, _eur), 2));
    }

    [Fact]
    public void Evolve_throws_for_an_unknown_event() =>
        Should.Throw<UnreachableException>(() => SampleLedger.Evolve(SampleLedger.Initial, new UnknownEvent()));

    private sealed record UnknownEvent : SampleLedgerEvent;
}
