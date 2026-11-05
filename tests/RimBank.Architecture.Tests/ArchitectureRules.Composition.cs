namespace RimBank.Architecture.Tests;

// The composition projects (tech-stack §2–§4, ADR 0014): RimBank.Host composes, RimBank.AppHost orchestrates, and
// RimBank.ServiceDefaults is applied by the host. Contexts and SharedKernel never reach up to any of them.
public static partial class ArchitectureRules
{
    private const string HostRoot = "RimBank.Host";
    private const string AppHostRoot = "RimBank.AppHost";
    private const string ServiceDefaultsRoot = "RimBank.ServiceDefaults";

    public static ArchitectureRule ServiceDefaultsOnlyInHost { get; } = new(
        "service-defaults-only-in-host",
        "tech-stack §1–§2 and ADR 0014: only RimBank.Host applies the service defaults; contexts never configure telemetry, health or resilience themselves",
        code => DependenciesOn(code, ServiceDefaultsRoot, allowedFrom: [HostRoot, ServiceDefaultsRoot]));

    public static ArchitectureRule NothingDependsOnAppHost { get; } = new(
        "nothing-depends-on-apphost",
        "tech-stack §1 and ADR 0014: RimBank.AppHost is local orchestration only; no project depends on it",
        code => DependenciesOn(code, AppHostRoot, allowedFrom: [AppHostRoot]));

    public static ArchitectureRule HostIsTheCompositionRoot { get; } = new(
        "host-is-the-composition-root",
        "tech-stack §2 and §4, ADR 0014: RimBank.Host references contexts, never the other way round; a context registers through a module class inside the host",
        code => DependenciesOn(code, HostRoot, allowedFrom: [HostRoot]));

    private static IEnumerable<Violation> DependenciesOn(CodeBase code, string root, IReadOnlyList<string> allowedFrom) =>
        from type in code.Types
        where !allowedFrom.Any(allowed => Namespaces.IsWithin(type.NamespaceName(), allowed))
        from target in code.Targets(type)
        where Namespaces.IsWithin(target.NamespaceName(), root)
        select new Violation(type.Display(), $"depends on {target.Display()} ({root})");
}
