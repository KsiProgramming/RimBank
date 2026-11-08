using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Identifies a party association (the Associations Behavior Qualifier, its own aggregate): the stream id of the
/// <c>PartyAssociation</c> aggregate and the <c>{associationsid}</c> route value (ADR 0024).
/// </summary>
public readonly record struct PartyAssociationId : ITypedId<PartyAssociationId>
{
    private PartyAssociationId(Guid value) => Value = value;

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public static PartyAssociationId New(TimeProvider time) => new(TypedId.NewGuid(time));

    /// <inheritdoc />
    public static PartyAssociationId From(Guid value) => new(TypedId.EnsureValid(value));

    /// <inheritdoc />
    public static Result<PartyAssociationId> TryCreate(Guid value) => TypedId.TryCreate<PartyAssociationId>(value);

    /// <inheritdoc />
    public static Result<PartyAssociationId> TryCreate(string? text) => TypedId.TryCreate<PartyAssociationId>(text);

    /// <inheritdoc />
    public override string ToString() => TypedId.Format(Value);

    /// <inheritdoc />
    public static bool operator <(PartyAssociationId left, PartyAssociationId right) => TypedId.Compare(left, right) < 0;

    /// <inheritdoc />
    public static bool operator <=(PartyAssociationId left, PartyAssociationId right) => TypedId.Compare(left, right) <= 0;

    /// <inheritdoc />
    public static bool operator >(PartyAssociationId left, PartyAssociationId right) => TypedId.Compare(left, right) > 0;

    /// <inheritdoc />
    public static bool operator >=(PartyAssociationId left, PartyAssociationId right) => TypedId.Compare(left, right) >= 0;
}
