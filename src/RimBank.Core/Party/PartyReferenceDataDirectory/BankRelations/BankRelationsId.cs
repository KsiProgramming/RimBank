using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Identifies the BankRelations Behavior Qualifier of an entry: one per entry, created when the entry is
/// registered, and the <c>{bankrelationsid}</c> route value (ADR 0024).
/// </summary>
public readonly record struct BankRelationsId : ITypedId<BankRelationsId>
{
    private BankRelationsId(Guid value) => Value = value;

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public static BankRelationsId New(TimeProvider time) => new(TypedId.NewGuid(time));

    /// <inheritdoc />
    public static BankRelationsId From(Guid value) => new(TypedId.EnsureValid(value));

    /// <inheritdoc />
    public static Result<BankRelationsId> TryCreate(Guid value) => TypedId.TryCreate<BankRelationsId>(value);

    /// <inheritdoc />
    public static Result<BankRelationsId> TryCreate(string? text) => TypedId.TryCreate<BankRelationsId>(text);

    /// <inheritdoc />
    public override string ToString() => TypedId.Format(Value);

    /// <inheritdoc />
    public static bool operator <(BankRelationsId left, BankRelationsId right) => TypedId.Compare(left, right) < 0;

    /// <inheritdoc />
    public static bool operator <=(BankRelationsId left, BankRelationsId right) => TypedId.Compare(left, right) <= 0;

    /// <inheritdoc />
    public static bool operator >(BankRelationsId left, BankRelationsId right) => TypedId.Compare(left, right) > 0;

    /// <inheritdoc />
    public static bool operator >=(BankRelationsId left, BankRelationsId right) => TypedId.Compare(left, right) >= 0;
}
