using ArchUnitNET.Domain;

namespace RimBank.Architecture.Tests;

// The CQRS rules (tech-stack §4 and §8, ADR 0003, ADR 0012) and the aggregate and projection contracts (ADR 0010).
public static partial class ArchitectureRules
{
    public static ArchitectureRule CommandHandlersStayOnWriteSide { get; } = new(
        "command-handlers-stay-on-write-side",
        "tech-stack §4 and ADR 0012: a command handler uses the write port and decisions, never a read port, a view, a query or a query handler",
        code =>
            from handler in code.Types
            where code.IsCommandHandler(handler)
            from target in code.Targets(handler)
            let problem = code.IsReadPort(target) ? "a read port"
                : code.IsView(target) ? "a view"
                : code.IsQuery(target) ? "a query"
                : code.IsQueryHandler(target) ? "a query handler"
                : null
            where problem is not null
            select new Violation(handler.Display(), $"depends on {target.Display()} ({problem})"));

    public static ArchitectureRule QueryHandlersStayOnReadSide { get; } = new(
        "query-handlers-stay-on-read-side",
        "tech-stack §4 and ADR 0012: a query handler uses the read port and views, never a write port, an aggregate, a command or a command handler",
        code =>
            from handler in code.Types
            where code.IsQueryHandler(handler)
            from target in code.Targets(handler)
            let problem = code.IsWritePort(target) ? "a write port"
                : code.IsAggregate(target) ? "an aggregate"
                : code.IsCommand(target) ? "a command"
                : code.IsCommandHandler(target) ? "a command handler"
                : null
            where problem is not null
            select new Violation(handler.Display(), $"depends on {target.Display()} ({problem})"));

    public static ArchitectureRule ProjectionsImplementContract { get; } = new(
        "projections-implement-contract",
        "ADR 0010: a view is projected by a sealed class named …Projection that implements IProjection",
        code => code.Types
            .Where(type => type is not Interface && (code.IsProjection(type) || IsNamedProjection(type)))
            .SelectMany(type => ProjectionContractProblems(code, type).Select(problem => new Violation(type.Display(), problem))));

    public static ArchitectureRule ProjectionsArePure { get; } = new(
        "projections-are-pure",
        "tech-stack §4 and §8.2: a projection is a pure (view, event) → view; it holds no state and never uses ports, handlers or TimeProvider",
        code => code.Types
            .Where(type => type is not Interface && (code.IsProjection(type) || IsNamedProjection(type)))
            .SelectMany(type => ImpurityProblems(code, type).Select(problem => new Violation(type.Display(), problem))));

    public static ArchitectureRule AggregatesImplementDecider { get; } = new(
        "aggregates-implement-decider",
        "tech-stack §7 and ADR 0010: an aggregate state is a sealed record in a core that implements IDecider",
        code => code.Types
            .Where(type => type is not Interface)
            .SelectMany(type => AggregateProblems(code, type).Select(problem => new Violation(type.Display(), problem))));

    private static bool IsNamedProjection(IType type) => type.Name.Split('`')[0].EndsWith("Projection", StringComparison.Ordinal);

    private static IEnumerable<string> ProjectionContractProblems(CodeBase code, IType type)
    {
        if (!code.IsProjection(type))
        {
            yield return "is named …Projection but does not implement IProjection<TView, TEvent>";
        }

        if (!IsNamedProjection(type))
        {
            yield return "implements IProjection but its name does not end with Projection";
        }

        if (type is not Class { IsSealed: true })
        {
            yield return "is not a sealed class";
        }
    }

    private static IEnumerable<string> ImpurityProblems(CodeBase code, IType type)
    {
        foreach (var field in type.Members.OfType<FieldMember>().Where(field => field.IsStatic != true))
        {
            yield return $"has the instance field {field.Name}";
        }

        foreach (var target in code.Targets(type))
        {
            var problem = target.FullName == "System.TimeProvider" ? "the clock; time comes from event data"
                : code.IsWritePort(target) || code.IsReadPort(target) ? "a port"
                : code.IsHandler(target) ? "a handler"
                : null;
            if (problem is not null)
            {
                yield return $"depends on {target.Display()} ({problem})";
            }
        }
    }

    private static IEnumerable<string> AggregateProblems(CodeBase code, IType type)
    {
        var hasEvolve = type.Members.OfType<MethodMember>().Any(method => method.IsStatic == true && method.Name.StartsWith("Evolve(", StringComparison.Ordinal));
        if (hasEvolve && !code.IsAggregate(type))
        {
            yield return "has a static Evolve but does not implement IDecider<TSelf, TCommand, TEvent>";
        }

        if (!code.IsAggregate(type))
        {
            yield break;
        }

        if (type is not Class { IsRecord: true, IsSealed: true })
        {
            yield return "implements IDecider but is not a sealed record";
        }

        if (ContextAddress.Parse(type.NamespaceName())?.Part != ContextPart.Core)
        {
            yield return "implements IDecider outside a context's core";
        }
    }
}
