using RimBank.SharedKernel;

namespace RimBank.Problems;

/// <summary>The errors the host reports itself, in the <c>shared</c> context of ADR 0006 (ADR 0015).</summary>
public static class SharedProblems
{
    /// <summary>
    /// The framework could not read the request into the endpoint's parameters, most often a body that is missing, is
    /// not JSON, or does not have the expected shape (400).
    /// </summary>
    public static Error RequestMalformed { get; } = Error.Validation(
        "shared.request-malformed",
        "The request could not be read. Check that its body is JSON of the expected shape, and its route values and headers.");

    /// <summary>No endpoint matches the request (404).</summary>
    public static Error NotFound { get; } = Error.NotFound("shared.not-found", "No resource exists at this address.");

    /// <summary>
    /// The request has no valid access token: none, malformed, expired, for another audience or badly signed (401).
    /// The response also carries <c>WWW-Authenticate: Bearer</c> (ADR 0019).
    /// </summary>
    public static Error Unauthenticated { get; } = Error.Unauthorized(
        "shared.unauthenticated",
        "This operation needs a valid access token. Send one in the Authorization header as 'Bearer <token>'.");

    /// <summary>The caller is authenticated but not allowed to perform this operation (403, ADR 0019).</summary>
    public static Error Forbidden { get; } =
        Error.Forbidden("shared.forbidden", "The access token does not allow this operation.");

    /// <summary>An exception reached the host (500). The response never says more than this.</summary>
    public static Error Unexpected { get; } =
        Error.Unexpected("shared.unexpected", "An unexpected error occurred. Quote the traceId when you report it.");
}
