using System.Diagnostics.CodeAnalysis;

namespace RimBank.SharedKernel;

/// <summary>
/// A strongly typed identifier: a <c>readonly record struct</c> wrapping a version 7 <see cref="Guid"/>
/// (tech-stack §7, ADR 0008).
/// </summary>
/// <typeparam name="TSelf">The identifier type itself, for example <c>PartyReferenceDataDirectoryEntryId</c>.</typeparam>
/// <remarks>
/// <para>
/// Each identifier is declared next to its aggregate and implements only the static members below, by forwarding
/// to <see cref="TypedId"/>. Parsing (<see cref="IParsable{TSelf}"/>, <see cref="ISpanParsable{TSelf}"/>),
/// formatting (<see cref="ISpanFormattable"/>) and ordering (<see cref="IComparable{TSelf}"/>) come from the
/// default implementations on this interface, so generic code such as model binding or <c>Order()</c> works.
/// </para>
/// <para>
/// Create new identifiers in the command handler (or the caller), never inside a pure <c>Decide</c> function.
/// </para>
/// <para>
/// Ordering follows creation time to the millisecond; identifiers created in the same millisecond have no
/// defined order.
/// </para>
/// </remarks>
public interface ITypedId<TSelf> : IEquatable<TSelf>, IComparable<TSelf>, ISpanParsable<TSelf>, ISpanFormattable
    where TSelf : struct, ITypedId<TSelf>
{
    /// <summary>The underlying version 7 GUID.</summary>
    Guid Value { get; }

    /// <summary>Creates a new identifier whose timestamp is the current time of <paramref name="time"/>.</summary>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "'New' is the name prescribed by ADR 0008; RimBank is C# only.")]
    static abstract TSelf New(TimeProvider time);

    /// <summary>Wraps a trusted value, for example one read back from the event store.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or not a version 7 GUID.</exception>
    static abstract TSelf From(Guid value);

    /// <summary>Wraps an untrusted value; a failure is a <see cref="ErrorKind.Validation"/> error.</summary>
    static abstract Result<TSelf> TryCreate(Guid value);

    /// <summary>Parses untrusted text (the <c>D</c> format); a failure is a <see cref="ErrorKind.Validation"/> error.</summary>
    static abstract Result<TSelf> TryCreate(string? text);

    /// <inheritdoc />
    int IComparable<TSelf>.CompareTo(TSelf other) => TypedId.Compare((TSelf)this, other);

    /// <inheritdoc />
    static TSelf IParsable<TSelf>.Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return TypedId.Parse<TSelf>(s);
    }

    /// <inheritdoc />
    static bool IParsable<TSelf>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TSelf result) =>
        TypedId.TryParse(s.AsSpan(), out result);

    /// <inheritdoc />
    static TSelf ISpanParsable<TSelf>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        TypedId.Parse<TSelf>(s);

    /// <inheritdoc />
    static bool ISpanParsable<TSelf>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TSelf result) =>
        TypedId.TryParse(s, out result);

    /// <inheritdoc />
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        TypedId.Format(Value, format);

    /// <inheritdoc />
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TypedId.TryFormat(Value, destination, out charsWritten, format);
}
