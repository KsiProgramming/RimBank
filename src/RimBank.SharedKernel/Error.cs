using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace RimBank.SharedKernel;

/// <summary>
/// An expected failure, returned as a value inside a <see cref="Result{T}"/> or <see cref="Result"/> rather than thrown.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Code"/> is stable and documented. It follows the convention <c>&lt;context&gt;.&lt;error-name&gt;</c>:
/// two lower-case kebab-case segments separated by a dot, for example <c>party.party-not-found</c> or
/// <c>shared.validation-failed</c>. Adapters turn it into the Problem Details type
/// <c>urn:rimbank:problem:&lt;context&gt;:&lt;error-name&gt;</c> and the <c>errorCode</c> extension (ADR 0006).
/// </para>
/// <para>
/// An invalid code or an empty message is a programmer error and throws <see cref="ArgumentException"/>.
/// </para>
/// </remarks>
public sealed partial record Error
{
    private const string CodeConvention = "<context>.<error-name>, lower-case kebab-case, for example 'party.party-not-found'";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _noFieldErrors =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>());

    private Error(string code, string message, ErrorKind kind, IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"Error code '{code}' must follow {CodeConvention}.", nameof(code));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        Kind = kind;
        FieldErrors = CopyOf(fieldErrors);
    }

    /// <summary>Stable error code, <c>&lt;context&gt;.&lt;error-name&gt;</c>.</summary>
    public string Code { get; }

    /// <summary>Human-readable explanation in business language. Never contains internal details.</summary>
    public string Message { get; }

    /// <summary>The category of failure, used by adapters to pick a status.</summary>
    public ErrorKind Kind { get; }

    /// <summary>Field name → messages for validation failures; empty otherwise.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; }

    /// <summary>The input is malformed or incomplete, optionally with per-field messages.</summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null) =>
        new(code, message, ErrorKind.Validation, fieldErrors);

    /// <summary>The requested record does not exist.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound, null);

    /// <summary>The request conflicts with the current state.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict, null);

    /// <summary>A business rule refuses an otherwise valid request.</summary>
    public static Error BusinessRule(string code, string message) => new(code, message, ErrorKind.BusinessRule, null);

    /// <summary>The caller is not authenticated.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized, null);

    /// <summary>The caller is not allowed to perform the operation.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden, null);

    /// <summary>A failure that is not the caller's fault.</summary>
    public static Error Unexpected(string code, string message) => new(code, message, ErrorKind.Unexpected, null);

    /// <summary>The operation needs a precondition the caller did not send.</summary>
    public static Error PreconditionRequired(string code, string message) => new(code, message, ErrorKind.PreconditionRequired, null);

    /// <inheritdoc />
    public bool Equals(Error? other) =>
        other is not null
        && Code == other.Code
        && Message == other.Message
        && Kind == other.Kind
        && FieldErrors.Count == other.FieldErrors.Count
        && FieldErrors.All(field =>
            other.FieldErrors.TryGetValue(field.Key, out var otherMessages)
            && field.Value.SequenceEqual(otherMessages));

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(Code, Message, Kind);
        foreach (var field in FieldErrors)
        {
            // XOR keeps the hash independent of dictionary order, matching Equals.
            hash ^= HashCode.Combine(field.Key, field.Value.Count);
        }

        return hash;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Kind} {Code}: {Message}";

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CopyOf(IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors)
    {
        if (fieldErrors is null || fieldErrors.Count == 0)
        {
            return _noFieldErrors;
        }

        var copy = new Dictionary<string, IReadOnlyList<string>>(fieldErrors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in fieldErrors)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                throw new ArgumentException("A field error needs a field name.", nameof(fieldErrors));
            }

            if (messages is null || messages.Count == 0)
            {
                throw new ArgumentException($"Field error '{field}' needs at least one message.", nameof(fieldErrors));
            }

            copy[field] = messages.ToArray().AsReadOnly();
        }

        return copy.AsReadOnly();
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
