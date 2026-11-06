using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// The key a caller sends with a command so that sending it again never changes state twice (mission quality bar
/// "Idempotency", specs/tech-stack.md §8.1, ADR 0018).
/// </summary>
/// <remarks>
/// <para>
/// The key is opaque to RimBank: 1 to <see cref="MaxLength"/> visible ASCII characters (<c>!</c> to <c>~</c>, no
/// spaces), as an HTTP <c>Idempotency-Key</c> header carries it. A UUID is a good choice for a caller; a
/// <c>.Subscriptions</c> adapter derives it from the requesting record's identifier (§8.4).
/// </para>
/// <para>
/// Every <see cref="ICommand"/> carries one. The key is scoped to its context and its command type: the same key on
/// two command types, or in two contexts, never clashes.
/// </para>
/// <para>
/// <c>default(IdempotencyKey)</c> cannot be prevented for a struct. It has no value and is not a key:
/// <see cref="TryCreate"/> never returns it, and a command that carries it is refused.
/// </para>
/// <para>JSON: a plain string.</para>
/// </remarks>
[JsonConverter(typeof(IdempotencyKeyJsonConverter))]
public readonly record struct IdempotencyKey
{
    /// <summary>The longest key accepted, in characters.</summary>
    public const int MaxLength = 255;

    /// <summary>Error code for a key that is empty, too long or has characters outside visible ASCII.</summary>
    public const string InvalidCode = "shared.idempotency-key-invalid";

    /// <summary>Error code for a command sent without a key.</summary>
    public const string MissingCode = "shared.idempotency-key-missing";

    /// <summary>Error code for a key already used with a different command payload.</summary>
    public const string ReusedCode = "shared.idempotency-key-reused";

    /// <summary>Error code for a key whose first command is still being handled.</summary>
    public const string InUseCode = "shared.idempotency-key-in-use";

    private static readonly Error _invalid = Error.Validation(
        InvalidCode,
        $"The idempotency key must be 1 to {MaxLength} visible ASCII characters, with no spaces. A UUID is a good choice.");

    private static readonly Error _missing = Error.Validation(
        MissingCode,
        "This operation changes state and needs an idempotency key (the Idempotency-Key header over HTTP).");

    private static readonly Error _reused = Error.Conflict(
        ReusedCode,
        "This idempotency key was already used for this operation with a different request. Use a new key for a new request.");

    private static readonly Error _inUse = Error.Conflict(
        InUseCode,
        "A request with this idempotency key is still being handled. Retry later with the same key to get its result.");

    private IdempotencyKey(string value) => Value = value;

    /// <summary>The key as the caller sent it.</summary>
    public string Value { get; }

    /// <summary>The error returned for a value that is not a valid key (Validation, 400).</summary>
    public static Error Invalid => _invalid;

    /// <summary>The error returned when a command arrives without a key (Validation, 400).</summary>
    public static Error Missing => _missing;

    /// <summary>The error returned when a key comes back with a different payload (Conflict, 409).</summary>
    public static Error Reused => _reused;

    /// <summary>The error returned when a key comes back while its first command is still being handled (Conflict, 409).</summary>
    public static Error InUse => _inUse;

    /// <summary>True for <c>default(IdempotencyKey)</c>, which is not a key.</summary>
    public bool IsDefault => Value is null;

    /// <summary>
    /// Accepts the value exactly as sent (case-sensitive, no trimming). Fails with <c>shared.idempotency-key-missing</c>
    /// for null and <c>shared.idempotency-key-invalid</c> (both Validation) for anything else that is not a key.
    /// </summary>
    public static Result<IdempotencyKey> TryCreate(string? value) =>
        value switch
        {
            null => _missing,
            _ when IsValid(value) => new IdempotencyKey(value),
            _ => _invalid,
        };

    /// <summary>Accepts a key from trusted input, for example a record's identifier in a <c>.Subscriptions</c> adapter or a test.</summary>
    /// <exception cref="ArgumentException">The value is not a valid key (a programmer error).</exception>
    public static IdempotencyKey From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValid(value)
            ? new IdempotencyKey(value)
            : throw new ArgumentException($"'{value}' is not an idempotency key: {_invalid.Message}", nameof(value));
    }

    /// <summary>The key, as sent (empty for <c>default(IdempotencyKey)</c>).</summary>
    public override string ToString() => Value ?? string.Empty;

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(character => character is >= '!' and <= '~');
}
