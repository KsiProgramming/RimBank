using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using RimBank.SharedKernel;

namespace RimBank.Api;

/// <summary>
/// A record's stream version as an HTTP entity tag (specs/tech-stack.md §5): commands and queries send it as a strong
/// <c>ETag</c> (<c>"3"</c>), a conditional <c>GET</c> whose <c>If-None-Match</c> already names it is answered
/// <c>304 Not Modified</c> (RFC 9110 §13.1.2), and a command on an existing record reads the version it expects from
/// <c>If-Match</c> (ADR 0027).
/// </summary>
public static class StreamVersionTag
{
    /// <summary>A command on an existing record was sent without <c>If-Match</c> (428, ADR 0027).</summary>
    public static Error IfMatchRequired { get; } = Error.PreconditionRequired(
        "shared.if-match-required",
        "This operation changes an existing record: send the ETag you last read in the If-Match header.");

    /// <summary>
    /// <c>If-Match</c> is not exactly one strong entity tag holding a stream version: weak, <c>*</c>, a list, or not a
    /// version this API issues (400, ADR 0027).
    /// </summary>
    public static Error IfMatchInvalid { get; } = Error.Validation(
        "shared.if-match-invalid",
        "The If-Match header is not valid.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [HeaderNames.IfMatch] = ["Send exactly one ETag, as returned by the last Retrieve or command, for example \"2\"."],
        });

    /// <summary>The entity tag of a stream version.</summary>
    /// <param name="version">The stream version, 1 or more.</param>
    /// <returns>The strong, quoted tag, for example <c>"3"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is below 1.</exception>
    public static string Format(long version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        return $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";
    }

    /// <summary>Sets the response's <c>ETag</c> header to the stream version.</summary>
    /// <param name="response">The response.</param>
    /// <param name="version">The stream version, 1 or more.</param>
    public static void SetOn(HttpResponse response, long version)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = Format(version);
    }

    /// <summary>
    /// Whether the request's <c>If-None-Match</c> names the current version, or is <c>*</c>, so the caller already
    /// holds it. The comparison is weak, as RFC 9110 requires for <c>If-None-Match</c>. A missing or unparsable header
    /// never matches.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="version">The record's current stream version.</param>
    /// <returns><see langword="true"/> when the answer is <c>304 Not Modified</c>.</returns>
    public static bool IsNotModified(HttpRequest request, long version)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = new EntityTagHeaderValue(Format(version));
        return request.GetTypedHeaders().IfNoneMatch
            .Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(current, useStrongComparison: false));
    }

    /// <summary>
    /// The stream version a command expects, from the request's <c>If-Match</c>. It must be exactly one strong tag
    /// holding a version of 1 or more, as <see cref="Format"/> writes it. <c>*</c> is refused: a command must name the
    /// version it decided against.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The expected version, <see cref="IfMatchRequired"/> or <see cref="IfMatchInvalid"/>.</returns>
    public static Result<long> ReadIfMatch(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = request.Headers[HeaderNames.IfMatch];
        return values.Count switch
        {
            0 => IfMatchRequired,
            1 when EntityTagHeaderValue.TryParse(values[0]?.Trim(), out var tag) && VersionOf(tag) is { } version => version,
            _ => IfMatchInvalid,
        };
    }

    // "<digits>" with no sign, no padding and no weak prefix, 1 or more.
    private static long? VersionOf(EntityTagHeaderValue tag) =>
        tag switch
        {
            { IsWeak: true } => null,
            _ when tag.Equals(EntityTagHeaderValue.Any) => null,
            _ when tag.Tag.Length > 2
                && tag.Tag.AsSpan(1, tag.Tag.Length - 2).IndexOfAnyExceptInRange('0', '9') < 0
                && long.TryParse(tag.Tag.AsSpan(1, tag.Tag.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                && version >= 1 => version,
            _ => null,
        };
}
