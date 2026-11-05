using System.Buffers.Binary;

namespace RimBank.SharedKernel;

/// <summary>
/// The shared logic behind every <see cref="ITypedId{TSelf}"/>: creation, validation, parsing and formatting.
/// </summary>
/// <remarks>
/// A valid identifier is a non-empty version 7 GUID (RFC 9562). Its text form is the lower-case <c>D</c> format,
/// for example <c>019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f</c>, with no prefix (ADR 0008).
/// </remarks>
public static class TypedId
{
    /// <summary>The error code for a value that is not a valid identifier (ADR 0006, ADR 0008).</summary>
    public const string InvalidIdentifierCode = "shared.invalid-identifier";

    private const string InvalidIdentifierMessage = "The value is not a valid identifier.";

    private static readonly Error _invalidIdentifier = Error.Validation(InvalidIdentifierCode, InvalidIdentifierMessage);

    /// <summary>A new version 7 GUID stamped with the current time of <paramref name="time"/>.</summary>
    public static Guid NewGuid(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        return Guid.CreateVersion7(time.GetUtcNow());
    }

    /// <summary>True when <paramref name="value"/> is a non-empty version 7 GUID.</summary>
    public static bool IsValid(Guid value) => value != Guid.Empty && value.Version == 7;

    /// <summary>Returns <paramref name="value"/> when it is valid; otherwise throws (trusted input only).</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or not a version 7 GUID.</exception>
    public static Guid EnsureValid(Guid value) =>
        IsValid(value)
            ? value
            : throw new ArgumentException("An identifier must be a non-empty version 7 GUID.", nameof(value));

    /// <summary>Wraps an untrusted value, or fails with <c>shared.invalid-identifier</c>.</summary>
    public static Result<TId> TryCreate<TId>(Guid value)
        where TId : struct, ITypedId<TId> =>
        IsValid(value) ? Result.Success(TId.From(value)) : Result.Failure<TId>(_invalidIdentifier);

    /// <summary>Parses untrusted text in the <c>D</c> format, or fails with <c>shared.invalid-identifier</c>.</summary>
    public static Result<TId> TryCreate<TId>(string? text)
        where TId : struct, ITypedId<TId> =>
        TryParse(text.AsSpan(), out TId id) ? Result.Success(id) : Result.Failure<TId>(_invalidIdentifier);

    /// <summary>Parses text in the <c>D</c> format.</summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid identifier.</exception>
    public static TId Parse<TId>(ReadOnlySpan<char> text)
        where TId : struct, ITypedId<TId> =>
        TryParse(text, out TId id) ? id : throw new FormatException(InvalidIdentifierMessage);

    /// <summary>Parses text in the <c>D</c> format; returns false when it is not a valid identifier.</summary>
    public static bool TryParse<TId>(ReadOnlySpan<char> text, out TId id)
        where TId : struct, ITypedId<TId>
    {
        if (Guid.TryParseExact(text, "D", out var value) && IsValid(value))
        {
            id = TId.From(value);
            return true;
        }

        id = default;
        return false;
    }

    /// <summary>The lower-case <c>D</c> text form of an identifier's value.</summary>
    public static string Format(Guid value) => value.ToString("D");

    /// <summary>
    /// The instant encoded in a version 7 identifier, to the millisecond. Informational only:
    /// business times always come from events, never from identifiers (ADR 0008).
    /// </summary>
    public static DateTimeOffset CreatedAt<TId>(TId id)
        where TId : struct, ITypedId<TId>
    {
        Span<byte> bytes = stackalloc byte[16];
        id.Value.TryWriteBytes(bytes, bigEndian: true, out _);
        var unixMilliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(bytes) >> 16);
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
    }
}
