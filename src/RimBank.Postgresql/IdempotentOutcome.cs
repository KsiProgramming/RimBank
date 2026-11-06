namespace RimBank.Postgresql;

/// <summary>
/// What a context remembers about a command it handled: the key it came with, a fingerprint of its payload and the
/// acknowledgement it returned (ADR 0018). Stored in the context's own schema, in the same transaction as the events
/// the command appended, so it is never committed without them or the other way round.
/// </summary>
/// <remarks>
/// Only successful commands are remembered: a failed decision commits nothing (ADR 0017), so the same key can be
/// retried. The payload itself is never stored, only its SHA-256 fingerprint. Marten records when the outcome was
/// written in its <c>mt_last_modified</c> metadata column.
/// </remarks>
public sealed class IdempotentOutcome
{
    /// <summary>
    /// <c>&lt;command type&gt;:&lt;caller length&gt;:&lt;caller&gt;:&lt;key&gt;</c>: the key is scoped to its command
    /// type and its caller within its context (ADR 0018, ADR 0019).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The hexadecimal SHA-256 of the command's JSON without its key.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>The acknowledgement the command returned, as JSON.</summary>
    public required string Acknowledgement { get; init; }
}
