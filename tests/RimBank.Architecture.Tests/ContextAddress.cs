namespace RimBank.Architecture.Tests;

/// <summary>The part of a bounded context a namespace belongs to (tech-stack §3.1).</summary>
public enum ContextPart
{
    Core,
    PublishedLanguage,
    Subscriptions,
    Api,
    Mcp,
    Postgresql,
}

/// <summary>
/// Where a namespace sits in the solution: <c>RimBank.&lt;BusinessDomain&gt;.&lt;ServiceDomain&gt;[.&lt;Adapter&gt;]</c>
/// (tech-stack §3.1, ADR 0011). Any other segment after the service domain is a business folder of the core.
/// </summary>
public sealed record ContextAddress(string BusinessDomain, string ServiceDomain, ContextPart Part)
{
    /// <summary>Projects that are not a bounded context: they hold no business domain (tech-stack §3.3).</summary>
    public static IReadOnlyList<string> CompositionProjects { get; } =
        ["RimBank.SharedKernel", "RimBank.Host", "RimBank.AppHost", "RimBank.ServiceDefaults", "RimBank.Problems", "RimBank.Api", "RimBank.Mcp", "RimBank.Postgresql"];

    /// <summary>The adapter suffixes a context project may have (tech-stack §3.1).</summary>
    public static IReadOnlyDictionary<string, ContextPart> AdapterSuffixes { get; } = new Dictionary<string, ContextPart>(StringComparer.Ordinal)
    {
        ["PublishedLanguage"] = ContextPart.PublishedLanguage,
        ["Subscriptions"] = ContextPart.Subscriptions,
        ["Api"] = ContextPart.Api,
        ["Mcp"] = ContextPart.Mcp,
        ["Postgresql"] = ContextPart.Postgresql,
    };

    /// <summary>The context's core namespace, for example <c>RimBank.Party.PartyReferenceDataDirectory</c>.</summary>
    public string Context => $"RimBank.{BusinessDomain}.{ServiceDomain}";

    /// <summary>Reads a namespace; <c>null</c> when it is not inside a bounded context.</summary>
    public static ContextAddress? Parse(string? namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName) || IsCompositionProject(namespaceName))
        {
            return null;
        }

        var segments = namespaceName.Split('.');
        if (segments.Length < 3 || segments[0] != "RimBank")
        {
            return null;
        }

        var part = segments.Length > 3 && AdapterSuffixes.TryGetValue(segments[3], out var adapter) ? adapter : ContextPart.Core;
        return new ContextAddress(segments[1], segments[2], part);
    }

    public static bool IsCompositionProject(string namespaceName) =>
        CompositionProjects.Any(project => Namespaces.IsWithin(namespaceName, project));
}
