using System.Diagnostics;
using RimBank.SharedKernel;

// A good context core: one aggregate, its commands and events, a write port, a read port, a view and its
// projection, one command flow and one query flow. Every architecture rule passes on it.
namespace RimBank.SampleBanking.SampleLedger;

public readonly record struct SampleLedgerEntryId : ITypedId<SampleLedgerEntryId>
{
    private SampleLedgerEntryId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SampleLedgerEntryId New(TimeProvider time) => new(TypedId.NewGuid(time));

    public static SampleLedgerEntryId From(Guid value) => new(TypedId.EnsureValid(value));

    public static Result<SampleLedgerEntryId> TryCreate(Guid value) => TypedId.TryCreate<SampleLedgerEntryId>(value);

    public static Result<SampleLedgerEntryId> TryCreate(string? text) => TypedId.TryCreate<SampleLedgerEntryId>(text);

    public override string ToString() => TypedId.Format(Value);

    public static bool operator <(SampleLedgerEntryId left, SampleLedgerEntryId right) => TypedId.Compare(left, right) < 0;

    public static bool operator <=(SampleLedgerEntryId left, SampleLedgerEntryId right) => TypedId.Compare(left, right) <= 0;

    public static bool operator >(SampleLedgerEntryId left, SampleLedgerEntryId right) => TypedId.Compare(left, right) > 0;

    public static bool operator >=(SampleLedgerEntryId left, SampleLedgerEntryId right) => TypedId.Compare(left, right) >= 0;
}

public abstract record SampleLedgerCommand;

public sealed record OpenSampleLedgerEntry(SampleLedgerEntryId Id, Currency Currency, IdempotencyKey IdempotencyKey) : SampleLedgerCommand, ICommand;

public sealed record PostSampleAmount(SampleLedgerEntryId Id, Money Amount, IdempotencyKey IdempotencyKey) : SampleLedgerCommand, ICommand;

public abstract record SampleLedgerEvent;

public sealed record SampleLedgerEntryOpened(SampleLedgerEntryId Id, Currency Currency) : SampleLedgerEvent;

public sealed record SampleAmountPosted(SampleLedgerEntryId Id, Money Amount) : SampleLedgerEvent;

public sealed record SampleLedgerEntry(SampleLedgerEntryId? Id, Money? Balance)
    : IDecider<SampleLedgerEntry, SampleLedgerCommand, SampleLedgerEvent>
{
    public static SampleLedgerEntry Initial { get; } = new(null, null);

    public static Result<IReadOnlyList<SampleLedgerEvent>> Decide(SampleLedgerCommand command, SampleLedgerEntry state) => (command, state.Balance) switch
    {
        (OpenSampleLedgerEntry open, null) => Decision.Accept<SampleLedgerEvent>(new SampleLedgerEntryOpened(open.Id, open.Currency)),
        (OpenSampleLedgerEntry, _) => Decision.Nothing<SampleLedgerEvent>(),
        (PostSampleAmount post, not null) => Decision.Accept<SampleLedgerEvent>(new SampleAmountPosted(post.Id, post.Amount)),
        (PostSampleAmount, null) => Error.Conflict("sample.ledger-not-open", "The sample ledger is not open."),
        _ => throw new UnreachableException(),
    };

    public static SampleLedgerEntry Evolve(SampleLedgerEntry state, SampleLedgerEvent domainEvent) => domainEvent switch
    {
        SampleLedgerEntryOpened opened => new SampleLedgerEntry(opened.Id, Money.Zero(opened.Currency)),
        SampleAmountPosted posted => state with { Balance = state.Balance + posted.Amount },
        _ => throw new UnreachableException(),
    };
}

/// <summary>Write port: loads and appends a sample ledger entry's event stream.</summary>
public interface ISampleLedgerDirectory : IWritePort
{
    Task<SampleLedgerEntry> Load(SampleLedgerEntryId id, CancellationToken cancellationToken);
}

/// <summary>Read port: reads sample ledger views.</summary>
public interface ISampleLedgerEnquiry : IReadPort
{
    Task<SampleLedgerEntryView?> Find(SampleLedgerEntryId id, CancellationToken cancellationToken);
}

public sealed record SampleLedgerEntryView(SampleLedgerEntryId Id, Money Balance, int Postings);

public sealed class SampleLedgerEntryViewProjection : IProjection<SampleLedgerEntryView, SampleLedgerEvent>
{
    private SampleLedgerEntryViewProjection()
    {
    }

    public static SampleLedgerEntryView? Project(SampleLedgerEntryView? view, SampleLedgerEvent domainEvent) => (view, domainEvent) switch
    {
        (null, SampleLedgerEntryOpened opened) => new SampleLedgerEntryView(opened.Id, Money.Zero(opened.Currency), 0),
        (null, _) => null,
        (_, SampleAmountPosted posted) => view with { Balance = view.Balance + posted.Amount, Postings = view.Postings + 1 },
        _ => view,
    };
}

public sealed class PostSampleAmountHandler(ISampleLedgerDirectory directory)
{
    public async Task<Result<IReadOnlyList<SampleLedgerEvent>>> HandleAsync(PostSampleAmount command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = await directory.Load(command.Id, cancellationToken).ConfigureAwait(false);
        return SampleLedgerEntry.Decide(command, state);
    }
}

public sealed record RetrieveSampleLedgerEntry(SampleLedgerEntryId Id) : IQuery<SampleLedgerEntryView>;

public sealed class RetrieveSampleLedgerEntryHandler(ISampleLedgerEnquiry enquiry)
{
    public async Task<Result<SampleLedgerEntryView>> HandleAsync(RetrieveSampleLedgerEntry query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var view = await enquiry.Find(query.Id, cancellationToken).ConfigureAwait(false);
        return view is null
            ? Error.NotFound("sample.ledger-not-found", "The sample ledger was not found.")
            : view;
    }
}
