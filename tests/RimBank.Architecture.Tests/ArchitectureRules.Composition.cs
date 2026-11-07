namespace RimBank.Architecture.Tests;

// The composition projects (tech-stack §2–§4, ADR 0014–0019): RimBank.Host composes, RimBank.AppHost orchestrates,
// RimBank.ServiceDefaults is applied by the host and alone opens endpoints (its health probes) to anonymous callers, RimBank.Problems maps errors for the adapters that answer callers,
// RimBank.Http holds the HTTP conventions every .Api follows, RimBank.Mcp the MCP conventions every .Mcp follows, and RimBank.Postgresql holds the Marten and Wolverine
// conventions every .Postgresql adapter follows.
// Contexts and SharedKernel never reach up to any of them.
public static partial class ArchitectureRules
{
    private const string HostRoot = "RimBank.Host";
    private const string AppHostRoot = "RimBank.AppHost";
    private const string ServiceDefaultsRoot = "RimBank.ServiceDefaults";
    private const string ProblemsRoot = "RimBank.Problems";
    private const string HttpRoot = "RimBank.Http";
    private const string SharedMcpRoot = "RimBank.Mcp";
    private const string SharedPostgresqlRoot = "RimBank.Postgresql";

    public static ArchitectureRule SharedPostgresqlOnlyInPostgresqlAdapters { get; } = new(
        "shared-postgresql-only-in-postgresql-adapters",
        "tech-stack §4, ADR 0016 and ADR 0017: only a context's .Postgresql adapter and the host (for its own message store) use RimBank.Postgresql; a core reaches its store through its ports, and the host reaches a context's store through the adapter's own extension methods",
        code =>
            from type in code.Types
            where !Namespaces.IsWithin(type.NamespaceName(), SharedPostgresqlRoot)
                && !Namespaces.IsWithin(type.NamespaceName(), HostRoot)
                && ContextAddress.Parse(type.NamespaceName())?.Part is not ContextPart.Postgresql
            from target in code.Targets(type)
            where Namespaces.IsWithin(target.NamespaceName(), SharedPostgresqlRoot)
            select new Violation(type.Display(), $"depends on {target.Display()} ({SharedPostgresqlRoot})"));

    public static ArchitectureRule SharedPostgresqlDependencies { get; } = new(
        "shared-postgresql-dependencies",
        "tech-stack §4, ADR 0016 and ADR 0017: RimBank.Postgresql references SharedKernel, Marten (with JasperFx, Weasel and Npgsql), Wolverine (its Marten and PostgreSQL integration) and the Microsoft.Extensions abstractions only, never a context, a composition project or ASP.NET Core",
        code =>
            from type in code.Types
            where Namespaces.IsWithin(type.NamespaceName(), SharedPostgresqlRoot)
            from target in code.Targets(type)
            where !IsBcl(target)
                && !new[] { "RimBank.SharedKernel", SharedPostgresqlRoot, Marten, "JasperFx", "Weasel", Npgsql, Wolverine, "Microsoft.Extensions" }
                    .Any(allowed => Namespaces.IsWithin(target.NamespaceName(), allowed))
            select new Violation(type.Display(), $"depends on {target.Display()}, which is not SharedKernel, Marten, JasperFx, Weasel, Npgsql, Wolverine, Microsoft.Extensions or the BCL"));

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

    public static ArchitectureRule ProblemsOnlyInAdaptersAndHost { get; } = new(
        "problems-only-in-adapters-and-host",
        "tech-stack §4, ADR 0015 and ADR 0022: only .Api, .Mcp, RimBank.Mcp and RimBank.Host turn an Error into Problem Details; a core returns the Error",
        code =>
            from type in code.Types
            where !Namespaces.IsWithin(type.NamespaceName(), HostRoot)
                && !Namespaces.IsWithin(type.NamespaceName(), ProblemsRoot)
                && !Namespaces.IsWithin(type.NamespaceName(), SharedMcpRoot)
                && ContextAddress.Parse(type.NamespaceName())?.Part is not (ContextPart.Api or ContextPart.Mcp)
            from target in code.Targets(type)
            where Namespaces.IsWithin(target.NamespaceName(), ProblemsRoot)
            select new Violation(type.Display(), $"depends on {target.Display()} ({ProblemsRoot})"));

    public static ArchitectureRule ProblemsDependencies { get; } = new(
        "problems-dependencies",
        "tech-stack §4 and ADR 0015: RimBank.Problems references SharedKernel and ASP.NET Core only",
        code =>
            from type in code.Types
            where Namespaces.IsWithin(type.NamespaceName(), ProblemsRoot)
            from target in code.Targets(type)
            where !IsBcl(target)
                && !Namespaces.IsWithin(target.NamespaceName(), AspNetCore)
                && !Namespaces.IsWithin(target.NamespaceName(), "Microsoft.Extensions")
                && !Namespaces.IsWithin(target.NamespaceName(), "RimBank.SharedKernel")
                && !Namespaces.IsWithin(target.NamespaceName(), ProblemsRoot)
            select new Violation(type.Display(), $"depends on {target.Display()}, which is not SharedKernel, ASP.NET Core or the BCL"));

    public static ArchitectureRule HttpOnlyInApiAndHost { get; } = new(
        "http-only-in-api-and-host",
        "tech-stack §4 and ADR 0018: only .Api and RimBank.Host use the HTTP conventions; a core receives the idempotency key on its command, and .Mcp has its own transport",
        code =>
            from type in code.Types
            where !Namespaces.IsWithin(type.NamespaceName(), HostRoot)
                && !Namespaces.IsWithin(type.NamespaceName(), HttpRoot)
                && ContextAddress.Parse(type.NamespaceName())?.Part is not ContextPart.Api
            from target in code.Targets(type)
            where Namespaces.IsWithin(target.NamespaceName(), HttpRoot)
            select new Violation(type.Display(), $"depends on {target.Display()} ({HttpRoot})"));

    public static ArchitectureRule HttpDependencies { get; } = new(
        "http-dependencies",
        "tech-stack §4 and ADR 0018: RimBank.Http references SharedKernel, RimBank.Problems and ASP.NET Core only, never a context, Marten or Wolverine",
        code =>
            from type in code.Types
            where Namespaces.IsWithin(type.NamespaceName(), HttpRoot)
            from target in code.Targets(type)
            where !IsBcl(target)
                && !new[] { AspNetCore, AspNetCoreHeaders, "Microsoft.Extensions", "RimBank.SharedKernel", ProblemsRoot, HttpRoot }
                    .Any(allowed => Namespaces.IsWithin(target.NamespaceName(), allowed))
            select new Violation(type.Display(), $"depends on {target.Display()}, which is not SharedKernel, RimBank.Problems, ASP.NET Core or the BCL"));

    public static ArchitectureRule SharedMcpOnlyInMcpAndHost { get; } = new(
        "shared-mcp-only-in-mcp-and-host",
        "tech-stack §4 and ADR 0022: only .Mcp and RimBank.Host use the MCP conventions; a core returns its Error, and .Api has its own transport",
        code =>
            from type in code.Types
            where !Namespaces.IsWithin(type.NamespaceName(), HostRoot)
                && !Namespaces.IsWithin(type.NamespaceName(), SharedMcpRoot)
                && ContextAddress.Parse(type.NamespaceName())?.Part is not ContextPart.Mcp
            from target in code.Targets(type)
            where Namespaces.IsWithin(target.NamespaceName(), SharedMcpRoot)
            select new Violation(type.Display(), $"depends on {target.Display()} ({SharedMcpRoot})"));

    public static ArchitectureRule SharedMcpDependencies { get; } = new(
        "shared-mcp-dependencies",
        "tech-stack §4 and ADR 0022: RimBank.Mcp references SharedKernel, RimBank.Problems, ASP.NET Core and the MCP C# SDK only, never a context, Marten or Wolverine",
        code =>
            from type in code.Types
            where Namespaces.IsWithin(type.NamespaceName(), SharedMcpRoot)
            from target in code.Targets(type)
            where !IsBcl(target)
                && !new[] { AspNetCore, "Microsoft.Extensions", ModelContextProtocol, "RimBank.SharedKernel", ProblemsRoot, SharedMcpRoot }
                    .Any(allowed => Namespaces.IsWithin(target.NamespaceName(), allowed))
            select new Violation(type.Display(), $"depends on {target.Display()}, which is not SharedKernel, RimBank.Problems, ASP.NET Core, the MCP C# SDK or the BCL"));

    public static ArchitectureRule AllowAnonymousOnlyInServiceDefaults { get; } = new(
        "allow-anonymous-only-in-service-defaults",
        "tech-stack §10 and ADR 0019: every endpoint needs an authenticated caller (deny by default); only the health probes of RimBank.ServiceDefaults (and, in Development, its OpenAPI document and Scalar UI) are open to anonymous callers, so no .Api, .Mcp or host code calls AllowAnonymous() or uses [AllowAnonymous]",
        code =>
            from type in code.Types
            where !Namespaces.IsWithin(type.NamespaceName(), ServiceDefaultsRoot)
            from anonymous in code.Targets(type).Where(target => _anonymousMarkers.Contains(target.FullName)).Select(target => target.Display())
                .Concat(code.CalledMethods(type).Where(IsAllowAnonymousCall).Select(method => $"{method.DeclaringType.Display()}.{method.Name}"))
                .Distinct(StringComparer.Ordinal)
            select new Violation(type.Display(), $"opens an endpoint to anonymous callers through {anonymous}"));

    private static readonly string[] _anonymousMarkers =
    [
        "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute",
        "Microsoft.AspNetCore.Authorization.IAllowAnonymous",
    ];

    // AllowAnonymous() sits next to RequireAuthorization() on the same extension class, so the rule matches the method.
    private static bool IsAllowAnonymousCall(ArchUnitNET.Domain.IMember method) =>
        method.Name.StartsWith("AllowAnonymous", StringComparison.Ordinal)
        && Namespaces.IsWithin(method.DeclaringType.NamespaceName(), AspNetCore);

    private static IEnumerable<Violation> DependenciesOn(CodeBase code, string root, IReadOnlyList<string> allowedFrom) =>
        from type in code.Types
        where !allowedFrom.Any(allowed => Namespaces.IsWithin(type.NamespaceName(), allowed))
        from target in code.Targets(type)
        where Namespaces.IsWithin(target.NamespaceName(), root)
        select new Violation(type.Display(), $"depends on {target.Display()} ({root})");
}
