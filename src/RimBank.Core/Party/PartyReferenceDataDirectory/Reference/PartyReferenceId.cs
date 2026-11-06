using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Reference;

/// <summary>
/// Identifies the Reference Behavior Qualifier of an entry: one per entry, created when the entry is
/// registered, and the <c>{referenceid}</c> route value (ADR 0024).
/// </summary>
public readonly record struct PartyReferenceId : ITypedId<PartyReferenceId>
{
    private PartyReferenceId(Guid value) => Value = value;

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public static PartyReferenceId New(TimeProvider time) => new(TypedId.NewGuid(time));

    /// <inheritdoc />
    public static PartyReferenceId From(Guid value) => new(TypedId.EnsureValid(value));

    /// <inheritdoc />
    public static Result<PartyReferenceId> TryCreate(Guid value) => TypedId.TryCreate<PartyReferenceId>(value);

    /// <inheritdoc />
    public static Result<PartyReferenceId> TryCreate(string? text) => TypedId.TryCreate<PartyReferenceId>(text);

    /// <inheritdoc />
    public override string ToString() => TypedId.Format(Value);

    /// <inheritdoc />
    public static bool operator <(PartyReferenceId left, PartyReferenceId right) => TypedId.Compare(left, right) < 0;

    /// <inheritdoc />
    public static bool operator <=(PartyReferenceId left, PartyReferenceId right) => TypedId.Compare(left, right) <= 0;

    /// <inheritdoc />
    public static bool operator >(PartyReferenceId left, PartyReferenceId right) => TypedId.Compare(left, right) > 0;

    /// <inheritdoc />
    public static bool operator >=(PartyReferenceId left, PartyReferenceId right) => TypedId.Compare(left, right) >= 0;
}
