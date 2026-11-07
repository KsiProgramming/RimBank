using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Identifies a Party Reference Data Directory entry (the Control Record): the stream id of the
/// <c>PartyReferenceDataDirectoryEntry</c> aggregate and the <c>{partyreferencedatadirectoryid}</c> route value (ADR 0024).
/// </summary>
public readonly record struct PartyReferenceDataDirectoryEntryId : ITypedId<PartyReferenceDataDirectoryEntryId>
{
    private PartyReferenceDataDirectoryEntryId(Guid value) => Value = value;

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public static PartyReferenceDataDirectoryEntryId New(TimeProvider time) => new(TypedId.NewGuid(time));

    /// <inheritdoc />
    public static PartyReferenceDataDirectoryEntryId From(Guid value) => new(TypedId.EnsureValid(value));

    /// <inheritdoc />
    public static Result<PartyReferenceDataDirectoryEntryId> TryCreate(Guid value) => TypedId.TryCreate<PartyReferenceDataDirectoryEntryId>(value);

    /// <inheritdoc />
    public static Result<PartyReferenceDataDirectoryEntryId> TryCreate(string? text) => TypedId.TryCreate<PartyReferenceDataDirectoryEntryId>(text);

    /// <inheritdoc />
    public override string ToString() => TypedId.Format(Value);

    /// <inheritdoc />
    public static bool operator <(PartyReferenceDataDirectoryEntryId left, PartyReferenceDataDirectoryEntryId right) => TypedId.Compare(left, right) < 0;

    /// <inheritdoc />
    public static bool operator <=(PartyReferenceDataDirectoryEntryId left, PartyReferenceDataDirectoryEntryId right) => TypedId.Compare(left, right) <= 0;

    /// <inheritdoc />
    public static bool operator >(PartyReferenceDataDirectoryEntryId left, PartyReferenceDataDirectoryEntryId right) => TypedId.Compare(left, right) > 0;

    /// <inheritdoc />
    public static bool operator >=(PartyReferenceDataDirectoryEntryId left, PartyReferenceDataDirectoryEntryId right) => TypedId.Compare(left, right) >= 0;
}
