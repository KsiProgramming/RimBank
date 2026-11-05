namespace RimBank.SharedKernel.DecidersAndProjections.Tests;

// Pure decisions of the sample ledger: Decide(command, state) → events or a business error.
public sealed partial record SampleLedger
{
    public const string AlreadyOpenedCode = "sample.ledger-already-opened";
    public const string NotOpenedCode = "sample.ledger-not-opened";
    public const string ClosedCode = "sample.ledger-closed";

    private static readonly Error _alreadyOpened = Error.Conflict(AlreadyOpenedCode, "The ledger is already open.");
    private static readonly Error _notOpened = Error.Conflict(NotOpenedCode, "The ledger has not been opened.");
    private static readonly Error _closed = Error.Conflict(ClosedCode, "The ledger is closed.");

    public static Result<IReadOnlyList<SampleLedgerEvent>> Decide(SampleLedgerCommand command, SampleLedger state) =>
        (command, state.Status) switch
        {
            (OpenSampleLedger open, SampleLedgerStatus.NotOpened) => Decision.Accept<SampleLedgerEvent>(new SampleLedgerOpened(open.Currency)),
            (OpenSampleLedger, _) => _alreadyOpened,
            (_, SampleLedgerStatus.NotOpened) => _notOpened,
            (PostToSampleLedger, SampleLedgerStatus.Closed) => _closed,
            (PostToSampleLedger post, _) => state.Balance!.Value.TryAdd(post.Amount)
                .Bind(_ => Decision.Accept<SampleLedgerEvent>(new SampleLedgerPosted(post.Amount))),
            (CloseSampleLedger, SampleLedgerStatus.Closed) => Decision.Nothing<SampleLedgerEvent>(),
            (CloseSampleLedger, _) => Decision.Accept<SampleLedgerEvent>(new SampleLedgerClosed()),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "SampleLedger does not know this command."),
        };
}
