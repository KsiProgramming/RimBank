namespace RimBank.SharedKernel;

/// <summary>
/// Who sent a request: the authenticated subject, and the client application it used (mission quality bar
/// "Auditability": every state change is traceable to an actor; ADR 0019).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Subject"/> is the opaque OpenID Connect <c>sub</c> of the caller's token, and <see cref="ClientId"/> its
/// <c>azp</c> (authorized party), when the token has one. Both are 1 to <see cref="MaxLength"/> visible ASCII characters
/// (<c>!</c> to <c>~</c>, no spaces). RimBank never interprets them.
/// </para>
/// <para>
/// <see cref="System"/> is the actor of a message that no caller sent, for example one a <c>.Subscriptions</c> adapter
/// or a scheduled message produces. Its subject contains a space, so no caller's subject can ever equal it.
/// </para>
/// <para>
/// <c>default(Actor)</c> cannot be prevented for a struct. It has no subject and is not an actor:
/// <see cref="TryCreate"/> never returns it.
/// </para>
/// </remarks>
public readonly record struct Actor
{
    /// <summary>The longest subject or client identifier accepted, in characters.</summary>
    public const int MaxLength = 255;

    /// <summary>Error code for a caller whose token has no usable subject or client identifier.</summary>
    public const string InvalidCode = "shared.actor-invalid";

    private static readonly Error _invalid = Error.Unauthorized(
        InvalidCode,
        $"The access token must identify its subject (and client, if any) with 1 to {MaxLength} visible ASCII characters.");

    private Actor(string subject, string? clientId)
    {
        Subject = subject;
        ClientId = clientId;
    }

    /// <summary>The actor of messages that no caller sent.</summary>
    public static Actor System { get; } = new("rimbank system", null);

    /// <summary>The error returned for a subject or client identifier that cannot identify a caller (Unauthorized, 401).</summary>
    public static Error Invalid => _invalid;

    /// <summary>The caller's subject, as its token carries it.</summary>
    public string Subject { get; }

    /// <summary>The client application the caller used, or <c>null</c> when the token does not say.</summary>
    public string? ClientId { get; }

    /// <summary>True for <see cref="System"/>.</summary>
    public bool IsSystem => this == System;

    /// <summary>True for <c>default(Actor)</c>, which is not an actor.</summary>
    public bool IsDefault => Subject is null;

    /// <summary>
    /// Accepts a caller's subject and client identifier exactly as sent (case-sensitive, no trimming). Fails with
    /// <c>shared.actor-invalid</c> (Unauthorized) when either is not 1 to <see cref="MaxLength"/> visible ASCII characters.
    /// </summary>
    /// <param name="subject">The token's <c>sub</c>.</param>
    /// <param name="clientId">The token's <c>azp</c>, or <c>null</c>.</param>
    /// <returns>The actor, or <see cref="Invalid"/>.</returns>
    public static Result<Actor> TryCreate(string? subject, string? clientId) =>
        subject is not null && IsValid(subject) && (clientId is null || IsValid(clientId))
            ? new Actor(subject, clientId)
            : _invalid;

    /// <summary>The subject, and the client in brackets when known.</summary>
    public override string ToString() => ClientId is null ? Subject ?? string.Empty : $"{Subject} ({ClientId})";

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(character => character is >= '!' and <= '~');
}
