using System.Diagnostics;
using LedgerSpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.SharedKernel.Tests.SampleLedger,
    RimBank.SharedKernel.Tests.SampleLedgerCommand,
    RimBank.SharedKernel.Tests.SampleLedgerEvent>;

namespace RimBank.SharedKernel.Tests;

// The sample ledger's decisions, written with the Given / When / Then helper (write side).
public sealed class SampleLedgerSpecificationTests
{
    private static readonly Currency _eur = Currency.From("EUR");
    private static readonly Money _tenEuros = Money.Of(10.00m, _eur);

    [Fact]
    public void Opening_a_new_ledger_decides_opened() =>
        LedgerSpecification.Given()
            .When(new OpenSampleLedger(_eur))
            .Then(new SampleLedgerOpened(_eur));

    [Fact]
    public void Opening_twice_is_a_conflict() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur))
            .When(new OpenSampleLedger(_eur))
            .ThenFails(ErrorKind.Conflict, SampleLedger.AlreadyOpenedCode);

    [Fact]
    public void Posting_to_an_open_ledger_decides_posted() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerPosted(_tenEuros))
            .When(new PostToSampleLedger(_tenEuros))
            .Then(new SampleLedgerPosted(_tenEuros));

    [Fact]
    public void Posting_before_opening_is_a_conflict() =>
        LedgerSpecification.Given()
            .When(new PostToSampleLedger(_tenEuros))
            .ThenFails(ErrorKind.Conflict, SampleLedger.NotOpenedCode);

    [Fact]
    public void Posting_to_a_closed_ledger_is_a_conflict() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerClosed())
            .When(new PostToSampleLedger(_tenEuros))
            .ThenFails(ErrorKind.Conflict, SampleLedger.ClosedCode);

    [Fact]
    public void Posting_another_currency_is_a_validation_error() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur))
            .When(new PostToSampleLedger(Money.Of(10.00m, Currency.From("USD"))))
            .ThenFails(ErrorKind.Validation, Money.CurrencyMismatchCode);

    [Fact]
    public void Closing_an_open_ledger_decides_closed() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur))
            .When(new CloseSampleLedger())
            .Then(new SampleLedgerClosed());

    [Fact]
    public void Closing_twice_is_an_accepted_no_op() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerClosed())
            .When(new CloseSampleLedger())
            .Then();

    [Fact]
    public void Evolve_folds_postings_into_the_balance() =>
        new SampleLedgerEvent[] { new SampleLedgerOpened(_eur), new SampleLedgerPosted(_tenEuros), new SampleLedgerPosted(Money.Of(-2.50m, _eur)) }
            .Aggregate(SampleLedger.Initial, SampleLedger.Evolve)
            .ShouldBe(new SampleLedger(SampleLedgerStatus.Open, Money.Of(7.50m, _eur), 2));

    [Fact]
    public void Evolve_throws_for_an_unknown_event() =>
        Should.Throw<UnreachableException>(() => SampleLedger.Evolve(SampleLedger.Initial, new UnknownEvent()));

    private sealed record UnknownEvent : SampleLedgerEvent;
}
