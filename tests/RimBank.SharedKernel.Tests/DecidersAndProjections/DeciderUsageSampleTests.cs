using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LedgerLaws = RimBank.SharedKernel.Testing.DeciderLaws<
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedger,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerCommand,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerEvent>;
using LedgerSpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedger,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerCommand,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerEvent>;
using LedgerViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerViewProjection,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerView,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerEvent>;

namespace RimBank.SharedKernel.DecidersAndProjections.Tests;

// The usage sample from ADR 0010, kept compiling and passing here: one test of each kind an aggregate and its
// view get (write side, read side, laws).
public sealed class DeciderUsageSampleTests
{
    private static readonly Currency _eur = Currency.From("EUR");
    private static readonly Money _tenEuros = Money.Of(10.00m, _eur);

    // Write side: Given events / When command / Then events (or ThenFails).
    [Fact]
    public void A_posting_to_an_open_ledger_is_recorded() =>
        LedgerSpecification.Given(new SampleLedgerOpened(_eur))
            .When(new PostToSampleLedger(_tenEuros))
            .Then(new SampleLedgerPosted(_tenEuros));

    // Read side: Given events / Then view.
    [Fact]
    public void The_view_shows_the_balance() =>
        LedgerViewSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerPosted(_tenEuros))
            .Then(new SampleLedgerView(_tenEuros, Postings: 1, IsClosed: false));

    // Laws: the read side always shows what the write side decided.
    [Property]
    public Property The_view_agrees_with_the_ledger() =>
        LedgerLaws.ProjectionAgreesWithState<SampleLedgerViewProjection, SampleLedgerView>(
            Gen.Elements<SampleLedgerCommand>(new OpenSampleLedger(_eur), new PostToSampleLedger(_tenEuros), new CloseSampleLedger()),
            ledger => ledger.Balance is { } balance
                ? new SampleLedgerView(balance, ledger.Postings, ledger.Status == SampleLedgerStatus.Closed)
                : null);
}
