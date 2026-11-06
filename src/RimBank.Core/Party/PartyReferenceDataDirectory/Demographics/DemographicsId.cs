using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Demographics;

/// <summary>
/// Identifies the Demographics Behavior Qualifier of an entry: one per entry, created when the entry is
/// registered, and the <c>{demographicsid}</c> route value (ADR 0024).
/// </summary>
public readonly record struct DemographicsId : ITypedId<DemographicsId>
{
    private DemographicsId(Guid value) => Value = value;

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public static DemographicsId New(TimeProvider time) => new(TypedId.NewGuid(time));

    /// <inheritdoc />
    public static DemographicsId From(Guid value) => new(TypedId.EnsureValid(value));

    /// <inheritdoc />
    public static Result<DemographicsId> TryCreate(Guid value) => TypedId.TryCreate<DemographicsId>(value);

    /// <inheritdoc />
    public static Result<DemographicsId> TryCreate(string? text) => TypedId.TryCreate<DemographicsId>(text);

    /// <inheritdoc />
    public override string ToString() => TypedId.Format(Value);

    /// <inheritdoc />
    public static bool operator <(DemographicsId left, DemographicsId right) => TypedId.Compare(left, right) < 0;

    /// <inheritdoc />
    public static bool operator <=(DemographicsId left, DemographicsId right) => TypedId.Compare(left, right) <= 0;

    /// <inheritdoc />
    public static bool operator >(DemographicsId left, DemographicsId right) => TypedId.Compare(left, right) > 0;

    /// <inheritdoc />
    public static bool operator >=(DemographicsId left, DemographicsId right) => TypedId.Compare(left, right) >= 0;
}
