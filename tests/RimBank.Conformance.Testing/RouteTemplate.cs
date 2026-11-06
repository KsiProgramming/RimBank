namespace RimBank.Conformance.Testing;

/// <summary>
/// A route or path template compared the way the harness matches routes (ADR 0020): literal segments ignore case,
/// every <c>{parameter}</c> matches any other parameter whatever its name, constraint or optional marker, and a
/// leading or trailing <c>/</c> does not matter. So <c>/PartyReferenceDataDirectory/{id:guid}/Retrieve</c> matches
/// BIAN's <c>/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve</c>.
/// </summary>
public sealed class RouteTemplate : IEquatable<RouteTemplate>
{
    private const string Parameter = "{}";

    private RouteTemplate(string text, string key)
    {
        Text = text;
        Key = key;
    }

    /// <summary>The template as written.</summary>
    public string Text { get; }

    /// <summary>The normalised form used for matching, for example <c>/PARTYREFERENCEDATADIRECTORY/{}/RETRIEVE</c>.</summary>
    public string Key { get; }

    /// <summary>The first literal segment as written, which for a BIAN route is the service domain; empty when there is none.</summary>
    public string FirstSegment => Segments(Text).FirstOrDefault() ?? string.Empty;

    /// <summary>Reads a route or path template.</summary>
    /// <param name="text">The template, for example <c>/PartyReferenceDataDirectory/{id}/Retrieve</c>.</param>
    /// <returns>The template.</returns>
    public static RouteTemplate Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        var key = "/" + string.Join('/', Segments(trimmed).Select(Normalise));
        return new RouteTemplate(trimmed, key);
    }

    /// <inheritdoc />
    public bool Equals(RouteTemplate? other) => other is not null && Key == other.Key;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RouteTemplate other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);

    /// <inheritdoc />
    public override string ToString() => Text;

    private static string[] Segments(string text) => text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalise(string segment) =>
        segment.StartsWith('{') && segment.EndsWith('}') ? Parameter : segment.ToUpperInvariant();
}
