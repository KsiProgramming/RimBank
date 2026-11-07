using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace RimBank.Api;

/// <summary>
/// A record's stream version as an HTTP entity tag (specs/tech-stack.md §5): commands and queries send it as a strong
/// <c>ETag</c> (<c>"3"</c>), and a conditional <c>GET</c> whose <c>If-None-Match</c> already names it is answered
/// <c>304 Not Modified</c> (RFC 9110 §13.1.2). Commands will read it back from <c>If-Match</c> (roadmap 1.8).
/// </summary>
public static class StreamVersionTag
{
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
}
