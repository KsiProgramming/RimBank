namespace RimBank.SharedKernel;

/// <summary>
/// The category of an expected failure. Adapters map each kind to one protocol status
/// (for example an HTTP status code) in a single place; see ADR 0006.
/// </summary>
public enum ErrorKind
{
    /// <summary>The input is malformed or incomplete (HTTP 400).</summary>
    Validation,

    /// <summary>The requested record does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>The request conflicts with the current state, for example a stale version or a reused idempotency key (HTTP 409).</summary>
    Conflict,

    /// <summary>The input is valid but a business rule refuses it, for example insufficient funds (HTTP 422).</summary>
    BusinessRule,

    /// <summary>The caller is not authenticated (HTTP 401).</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not allowed to do this (HTTP 403).</summary>
    Forbidden,

    /// <summary>An expected-but-unrecoverable failure that is not the caller's fault (HTTP 500).</summary>
    Unexpected,
}
