namespace RimBank.SharedKernel.TypedIds.Tests;

// The declaration template from ADR 0008. A real identifier lives next to its aggregate,
// for example PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntryId.cs.
public readonly record struct SampleId : ITypedId<SampleId>
{
    private SampleId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SampleId New(TimeProvider time) => new(TypedId.NewGuid(time));

    public static SampleId From(Guid value) => new(TypedId.EnsureValid(value));

    public static Result<SampleId> TryCreate(Guid value) => TypedId.TryCreate<SampleId>(value);

    public static Result<SampleId> TryCreate(string? text) => TypedId.TryCreate<SampleId>(text);

    public override string ToString() => TypedId.Format(Value);

    public static bool operator <(SampleId left, SampleId right) => TypedId.Compare(left, right) < 0;

    public static bool operator <=(SampleId left, SampleId right) => TypedId.Compare(left, right) <= 0;

    public static bool operator >(SampleId left, SampleId right) => TypedId.Compare(left, right) > 0;

    public static bool operator >=(SampleId left, SampleId right) => TypedId.Compare(left, right) >= 0;
}
