using ArchUnitNET.Domain;

namespace RimBank.Architecture.Tests;

// The dependency table (tech-stack §4) and cross-context isolation (tech-stack §8.4, ADR 0004).
// The table rules look inside one context and at frameworks; every reference to another context is judged by
// cross-context-isolation alone, so one mistake breaks one rule.
public static partial class ArchitectureRules
{
    private const string AspNetCore = "Microsoft.AspNetCore";

    // The typed HTTP headers (ETag, If-None-Match) of the ASP.NET Core shared framework, outside its Microsoft.AspNetCore
    // namespaces.
    private const string AspNetCoreHeaders = "Microsoft.Net.Http.Headers";
    private const string Marten = "Marten";
    private const string Wolverine = "Wolverine";
    private const string WolverineMarten = "Wolverine.Marten";
    private const string MessageBus = "Wolverine.IMessageBus";

    /// <summary>
    /// <see cref="MessageBus"/> and the interfaces it is made of: calling <c>bus.InvokeAsync&lt;T&gt;(…)</c> on an
    /// <c>IMessageBus</c> compiles to a call on the interface that declares it (<c>ICommandBus</c>).
    /// <c>The_message_bus_surface_is_what_IMessageBus_is_made_of</c> keeps this list equal to Wolverine's.
    /// </summary>
    public static IReadOnlySet<string> MessageBusSurface { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        MessageBus,
        "Wolverine.ICommandBus",
    };
    private const string Npgsql = "Npgsql";
    private const string ModelContextProtocol = "ModelContextProtocol";

    public static ArchitectureRule SharedKernelDependencies { get; } = new(
        "shared-kernel-dependencies",
        "tech-stack §4: RimBank.SharedKernel references the BCL only",
        code => code.Types
            .Where(type => Namespaces.IsWithin(type.NamespaceName(), "RimBank.SharedKernel"))
            .SelectMany(type => code.Targets(type)
                .Where(target => !IsBcl(target) && !Namespaces.IsWithin(target.NamespaceName(), "RimBank.SharedKernel"))
                .Select(target => new Violation(type.Display(), $"depends on {target.Display()}, which is not in the BCL"))));

    public static ArchitectureRule CoreDependencies { get; } = new(
        "core-dependencies",
        "tech-stack §4: a core references SharedKernel and its own .PublishedLanguage only, never a framework or its own adapters",
        code => PartDependencies(
            code,
            ContextPart.Core,
            target => IsBcl(target) || IsRimBank(target) ? null : "a technical package",
            [ContextPart.Api, ContextPart.Mcp, ContextPart.Postgresql, ContextPart.Subscriptions]));

    public static ArchitectureRule PublishedLanguageDependencies { get; } = new(
        "published-language-dependencies",
        "tech-stack §4: .PublishedLanguage references the BCL only",
        code => PartDependencies(
            code,
            ContextPart.PublishedLanguage,
            target => IsBcl(target) ? null : "not in the BCL",
            [ContextPart.Core, ContextPart.Api, ContextPart.Mcp, ContextPart.Postgresql, ContextPart.Subscriptions]));

    public static ArchitectureRule SubscriptionsDependencies { get; } = new(
        "subscriptions-dependencies",
        "tech-stack §4: .Subscriptions references its own core, SharedKernel, other contexts' .PublishedLanguage and Wolverine, never its own .Api, .Mcp or .Postgresql",
        code => PartDependencies(code, ContextPart.Subscriptions, Banned(AspNetCore, Marten, WolverineMarten, Npgsql, ModelContextProtocol), [ContextPart.Api, ContextPart.Mcp, ContextPart.Postgresql]));

    public static ArchitectureRule ApiDependencies { get; } = new(
        "api-dependencies",
        "tech-stack §4: .Api references its own core, SharedKernel, ASP.NET Core and Wolverine's IMessageBus, never .Postgresql",
        code => PartDependencies(code, ContextPart.Api, Banned(Marten, WolverineMarten, Npgsql, ModelContextProtocol), [ContextPart.Postgresql]));

    public static ArchitectureRule McpDependencies { get; } = new(
        "mcp-dependencies",
        "tech-stack §4 and §6: .Mcp references its own core, SharedKernel, the MCP C# SDK and Wolverine's IMessageBus, never .Api or .Postgresql",
        code => PartDependencies(code, ContextPart.Mcp, Banned(Marten, WolverineMarten, Npgsql), [ContextPart.Api, ContextPart.Postgresql]));

    // Wolverine.Marten is left to api-dependencies and mcp-dependencies, so one mistake breaks one rule.
    public static ArchitectureRule AdaptersDispatchThroughTheMessageBus { get; } = new(
        "adapters-dispatch-through-the-message-bus",
        "tech-stack §4 and §6, ADR 0017: .Api and .Mcp use Wolverine only through IMessageBus; the host and each context's .Postgresql configure Wolverine",
        code =>
            from type in code.Types
            where ContextAddress.Parse(type.NamespaceName())?.Part is ContextPart.Api or ContextPart.Mcp
            from target in code.Targets(type)
            where Namespaces.IsWithin(target.NamespaceName(), Wolverine)
                && !Namespaces.IsWithin(target.NamespaceName(), WolverineMarten)
                && !MessageBusSurface.Contains(target.FullName)
            select new Violation(type.Display(), $"depends on {target.Display()}, which is not {MessageBus}"));

    public static ArchitectureRule PostgresqlDependencies { get; } = new(
        "postgresql-dependencies",
        "tech-stack §4: .Postgresql references its own core, SharedKernel, Marten and Wolverine.Marten, never .Api or .Mcp",
        code => PartDependencies(code, ContextPart.Postgresql, Banned(AspNetCore, ModelContextProtocol), [ContextPart.Api, ContextPart.Mcp]));

    public static ArchitectureRule CrossContextIsolation { get; } = new(
        "cross-context-isolation",
        "tech-stack §8.4 and ADR 0004: the only reference to another context is .Subscriptions → its .PublishedLanguage",
        code =>
            from type in code.Types
            let origin = ContextAddress.Parse(type.NamespaceName())
            where origin is not null
            from target in code.Targets(type)
            let destination = ContextAddress.Parse(target.NamespaceName())
            where destination is not null
                && destination.Context != origin.Context
                && !(origin.Part == ContextPart.Subscriptions && destination.Part == ContextPart.PublishedLanguage)
            select new Violation(type.Display(), $"depends on {target.Display()} ({destination.Part} of {destination.Context})"));

    public static ArchitectureRule PublishedLanguageHasNoViews { get; } = new(
        "published-language-has-no-views",
        "tech-stack §3.1 and §8.4: .PublishedLanguage holds integration events only; views are private to their context",
        code => code.Types
            .Where(type => ContextAddress.Parse(type.NamespaceName())?.Part == ContextPart.PublishedLanguage && code.IsView(type))
            .Select(type => new Violation(type.Display(), "is a view (a projection returns it) but lives in .PublishedLanguage")));

    private static IEnumerable<Violation> PartDependencies(
        CodeBase code,
        ContextPart part,
        Func<IType, string?> frameworkProblem,
        IReadOnlyCollection<ContextPart> forbiddenOwnParts) =>
        from type in code.Types
        let origin = ContextAddress.Parse(type.NamespaceName())
        where origin?.Part == part
        from target in code.Targets(type)
        let destination = ContextAddress.Parse(target.NamespaceName())
        let problem = destination is null
            ? ProblemOutsideContexts(target, part, frameworkProblem)
            : destination.Context == origin.Context && forbiddenOwnParts.Contains(destination.Part) ? $"its own {destination.Part}" : null
        where problem is not null
        select new Violation(type.Display(), $"depends on {target.Display()} ({problem})");

    private static string? ProblemOutsideContexts(IType target, ContextPart part, Func<IType, string?> frameworkProblem) =>
        part is ContextPart.PublishedLanguage && Namespaces.IsWithin(target.NamespaceName(), "RimBank")
            ? "RimBank code outside the published language"
            : frameworkProblem(target);

    private static Func<IType, string?> Banned(params string[] frameworks) =>
        target => frameworks.FirstOrDefault(framework => Namespaces.IsWithin(target.NamespaceName(), framework)) is { } banned
            ? $"{banned} is not allowed here"
            : null;

    private static bool IsBcl(IType type) => Namespaces.IsWithin(type.NamespaceName(), "System") || type.NamespaceName().Length == 0;

    private static bool IsRimBank(IType type) => Namespaces.IsWithin(type.NamespaceName(), "RimBank");
}
