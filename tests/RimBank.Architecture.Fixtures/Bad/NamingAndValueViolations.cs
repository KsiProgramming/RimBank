using RimBank.SharedKernel;

// Bad contexts for the naming and value rules (tech-stack §3, ADR 0008, ADR 0009, ADR 0011).
// Each one breaks exactly one rule, named above it.

// namespace-rule: the business domain is not in the service domain catalogue (tech-stack §3.2).
namespace RimBank.UnknownBanking.StrayLedger
{
    public sealed record StrayLedgerEntry(Guid Id);
}

// business-folder-names: no technical folders such as Services in a core (tech-stack §3.4).
namespace RimBank.SampleBanking.FolderNamedServices.Services
{
    public sealed record FolderNamedServicesEntry(Guid Id);
}

// no-binary-floating-point: amounts and rates are decimal, never double or float (ADR 0009).
namespace RimBank.SampleBanking.FloatingLedger
{
    public sealed record FloatingLedgerLine(double Amount);

    public static class FloatingLedgerRates
    {
        public static decimal Average(IReadOnlyList<float> history) => history is null ? 0m : (decimal)history.Average();
    }
}

// typed-ids-are-readonly-record-structs: a typed ID is readonly (ADR 0008).
namespace RimBank.SampleBanking.TypedIdNotReadOnly
{
#pragma warning disable IDE0250 // The missing readonly is the violation this fixture shows.
    public record struct TypedIdNotReadOnlyId : ITypedId<TypedIdNotReadOnlyId>
#pragma warning restore IDE0250
    {
        private TypedIdNotReadOnlyId(Guid value) => Value = value;

        public Guid Value { get; }

        public static TypedIdNotReadOnlyId New(TimeProvider time) => new(TypedId.NewGuid(time));

        public static TypedIdNotReadOnlyId From(Guid value) => new(TypedId.EnsureValid(value));

        public static Result<TypedIdNotReadOnlyId> TryCreate(Guid value) => TypedId.TryCreate<TypedIdNotReadOnlyId>(value);

        public static Result<TypedIdNotReadOnlyId> TryCreate(string? text) => TypedId.TryCreate<TypedIdNotReadOnlyId>(text);

        public override readonly string ToString() => TypedId.Format(Value);

        public static bool operator <(TypedIdNotReadOnlyId left, TypedIdNotReadOnlyId right) => TypedId.Compare(left, right) < 0;

        public static bool operator <=(TypedIdNotReadOnlyId left, TypedIdNotReadOnlyId right) => TypedId.Compare(left, right) <= 0;

        public static bool operator >(TypedIdNotReadOnlyId left, TypedIdNotReadOnlyId right) => TypedId.Compare(left, right) > 0;

        public static bool operator >=(TypedIdNotReadOnlyId left, TypedIdNotReadOnlyId right) => TypedId.Compare(left, right) >= 0;
    }
}

// typed-ids-are-readonly-record-structs: a typed ID is a record struct (ADR 0008).
namespace RimBank.SampleBanking.TypedIdNotRecord
{
    public readonly struct TypedIdNotRecordId : ITypedId<TypedIdNotRecordId>
    {
        private TypedIdNotRecordId(Guid value) => Value = value;

        public Guid Value { get; }

        public static TypedIdNotRecordId New(TimeProvider time) => new(TypedId.NewGuid(time));

        public static TypedIdNotRecordId From(Guid value) => new(TypedId.EnsureValid(value));

        public static Result<TypedIdNotRecordId> TryCreate(Guid value) => TypedId.TryCreate<TypedIdNotRecordId>(value);

        public static Result<TypedIdNotRecordId> TryCreate(string? text) => TypedId.TryCreate<TypedIdNotRecordId>(text);

        public bool Equals(TypedIdNotRecordId other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is TypedIdNotRecordId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => TypedId.Format(Value);

        public static bool operator ==(TypedIdNotRecordId left, TypedIdNotRecordId right) => left.Equals(right);

        public static bool operator !=(TypedIdNotRecordId left, TypedIdNotRecordId right) => !left.Equals(right);

        public static bool operator <(TypedIdNotRecordId left, TypedIdNotRecordId right) => TypedId.Compare(left, right) < 0;

        public static bool operator <=(TypedIdNotRecordId left, TypedIdNotRecordId right) => TypedId.Compare(left, right) <= 0;

        public static bool operator >(TypedIdNotRecordId left, TypedIdNotRecordId right) => TypedId.Compare(left, right) > 0;

        public static bool operator >=(TypedIdNotRecordId left, TypedIdNotRecordId right) => TypedId.Compare(left, right) >= 0;
    }
}
