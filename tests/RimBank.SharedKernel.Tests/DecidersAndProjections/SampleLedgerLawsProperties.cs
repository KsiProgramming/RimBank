using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using RimBank.SharedKernel.Testing;
using LedgerLaws = RimBank.SharedKernel.Testing.DeciderLaws<
    RimBank.SharedKernel.Tests.SampleLedger,
    RimBank.SharedKernel.Tests.SampleLedgerCommand,
    RimBank.SharedKernel.Tests.SampleLedgerEvent>;

namespace RimBank.SharedKernel.Tests;

// The decider laws and the projection/aggregate agreement law on the sample ledger. Each runs 100 command sequences.
public sealed class SampleLedgerLawsProperties
{
    // Three currencies with 2, 0 and 3 minor units: few enough that postings often match the ledger's currency,
    // so both accepted postings and currency mismatches are common.
    private static readonly Gen<Currency> _currencies =
        Gen.Elements(Currency.From("EUR"), Currency.From("JPY"), Currency.From("BHD"));

    // Mostly postings, with opens and closes mixed in, so sequences reach every state and every rejection.
    private static readonly Gen<SampleLedgerCommand> _commands = Gen.Frequency(
        (1, _currencies.Select(currency => (SampleLedgerCommand)new OpenSampleLedger(currency))),
        (6, _currencies.SelectMany(MoneyArbitraries.MoneyGen).Select(amount => (SampleLedgerCommand)new PostToSampleLedger(amount))),
        (1, Gen.Constant((SampleLedgerCommand)new CloseSampleLedger())));

    [Property]
    public Property Decisions_are_deterministic() => LedgerLaws.DecisionsAreDeterministic(_commands);

    [Property]
    public Property Decided_events_evolve() => LedgerLaws.DecidedEventsEvolve(_commands);

    [Property]
    public Property The_view_agrees_with_the_ledger() =>
        LedgerLaws.ProjectionAgreesWithState<SampleLedgerViewProjection, SampleLedgerView>(_commands, ExpectedView);

    // What the read side must show for a given write-side state.
    internal static SampleLedgerView? ExpectedView(SampleLedger ledger) => ledger.Balance switch
    {
        null => null,
        Money balance => new SampleLedgerView(balance, ledger.Postings, ledger.Status == SampleLedgerStatus.Closed),
    };
}
