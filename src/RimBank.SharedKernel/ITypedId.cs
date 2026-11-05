using System.Diagnostics.CodeAnalysis;

namespace RimBank.SharedKernel;

/// <summary>
/// A strongly typed identifier: a <c>readonly record struct</c> wrapping a version 7 <see cref="Guid"/>
/// (tech-stack §7, ADR 0008).
/// </summary>
/// <typeparam name="TSelf">The identifier type itself, for example <c>PartyReferenceDataDirectoryEntryId</c>.</typeparam>
/// <remarks>
/// Each identifier is declared next to its aggregate and forwards to <see cref="TypedId"/>.
/// Create new identifiers in the command handler (or the caller), never inside a pure <c>Decide</c> function.
/// </remarks>
public interface ITypedId<TSelf>
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
}
