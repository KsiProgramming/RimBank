using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;

namespace RimBank.Architecture.Tests;

/// <summary>
/// The code the rules look at: the production types and test assemblies of one loaded ArchUnitNET
/// <see cref="ArchUnitNET.Domain.Architecture"/>, with the CQRS classification of ADR 0012.
/// </summary>
public sealed class CodeBase
{
    private const string CommandMarker = "RimBank.SharedKernel.ICommand";
    private const string QueryMarker = "RimBank.SharedKernel.IQuery`1";
    private const string WritePortMarker = "RimBank.SharedKernel.IWritePort";
    private const string ReadPortMarker = "RimBank.SharedKernel.IReadPort";
    private const string DeciderContract = "RimBank.SharedKernel.IDecider`3";
    private const string ProjectionContract = "RimBank.SharedKernel.IProjection`2";
    private const string TypedIdContract = "RimBank.SharedKernel.ITypedId`1";

    private readonly Dictionary<string, List<IType>> _generatedByOwner;
    private readonly HashSet<string> _commandHandlers;
    private readonly HashSet<string> _queryHandlers;
    private readonly HashSet<string> _views;

    public CodeBase(
        string name,
        IEnumerable<IType> productionTypes,
        IEnumerable<(string Assembly, IReadOnlyList<IType> Types)> testAssemblies,
        ServiceDomainCatalogue catalogue)
    {
        Name = name;
        Catalogue = catalogue;
        var all = productionTypes.ToList();
        Types = [.. all.Where(type => !type.IsToolGenerated())];
        TestAssemblies = [.. testAssemblies.Select(assembly => (assembly.Assembly, (IReadOnlyList<IType>)[.. assembly.Types.Where(type => !type.IsToolGenerated())]))];

        var names = Types.Select(type => type.FullName).ToHashSet(StringComparer.Ordinal);
        _generatedByOwner = all
            .Where(type => type.IsToolGenerated())
            .Select(type => (Owner: OwnerOf(type.FullName, names), Type: type))
            .Where(pair => pair.Owner is not null)
            .GroupBy(pair => pair.Owner!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Type).ToList(), StringComparer.Ordinal);

        _commandHandlers = [.. Types.Where(type => HandledMessage(type) is { } message && IsCommand(message)).Select(type => type.FullName)];
        _queryHandlers = [.. Types.Where(type => HandledMessage(type) is { } message && IsQuery(message)).Select(type => type.FullName)];
        _views = [.. Types.Where(IsProjection).SelectMany(ProjectedViews).Select(type => type.FullName)];
    }

    public string Name { get; }

    public ServiceDomainCatalogue Catalogue { get; }

    /// <summary>Production types written by people (compiler- and tool-generated types are folded into their owner).</summary>
    public IReadOnlyList<IType> Types { get; }

    /// <summary>The test assemblies (<c>*.Tests</c>) and their types.</summary>
    public IReadOnlyList<(string Assembly, IReadOnlyList<IType> Types)> TestAssemblies { get; }

#pragma warning disable CA1822 // One classification API for rules: marker checks sit with the handler and view checks, which need state.
    public bool IsCommand(IType type) => type.IsAssignableTo(CommandMarker);

    public bool IsQuery(IType type) => type.IsAssignableTo(QueryMarker);

    public bool IsWritePort(IType type) => type.IsAssignableTo(WritePortMarker);

    public bool IsReadPort(IType type) => type.IsAssignableTo(ReadPortMarker);

    public bool IsAggregate(IType type) => type is not Interface && type.IsAssignableTo(DeciderContract);

    public bool IsProjection(IType type) => type is not Interface && type.IsAssignableTo(ProjectionContract);

    public bool IsTypedId(IType type) => type is not Interface && type.IsAssignableTo(TypedIdContract);
#pragma warning restore CA1822

    public bool IsCommandHandler(IType type) => _commandHandlers.Contains(type.FullName);

    public bool IsQueryHandler(IType type) => _queryHandlers.Contains(type.FullName);

    public bool IsHandler(IType type) => IsCommandHandler(type) || IsQueryHandler(type);

    /// <summary>A view is a type a projection's <c>Project</c> returns (ADR 0010, ADR 0012).</summary>
    public bool IsView(IType type) => _views.Contains(type.FullName);

    /// <summary>
    /// Every type <paramref name="type"/> uses: its own dependencies, the generic arguments in them, and those of the
    /// compiler-generated types it owns (lambdas, async and iterator state machines).
    /// </summary>
    public IReadOnlyList<IType> Targets(IType type)
    {
        var sources = _generatedByOwner.TryGetValue(type.FullName, out var generated) ? [type, .. generated] : new List<IType> { type };
        return [.. sources
            .SelectMany(source => source.Dependencies)
            .SelectMany(TargetsOf)
            .Where(target => !target.IsGenericParameter && !target.IsToolGenerated() && target.FullName != type.FullName)
            .DistinctBy(target => target.FullName)];
    }

    private static IEnumerable<IType> TargetsOf(ITypeDependency dependency) =>
        [dependency.Target, .. dependency.TargetGenericArguments.SelectMany(TypesOf)];

    private static IEnumerable<IType> TypesOf(GenericArgument argument) =>
        [argument.Type, .. argument.GenericArguments.SelectMany(TypesOf)];

    /// <summary>The first parameter of a <c>Handle</c> / <c>HandleAsync</c> method: the message a handler handles.</summary>
    private static IType? HandledMessage(IType type) =>
        type is Class
            ? type.Members.OfType<MethodMember>()
                .Where(method => method.Name.StartsWith("Handle(", StringComparison.Ordinal) || method.Name.StartsWith("HandleAsync(", StringComparison.Ordinal))
                .Select(method => method.Parameters.FirstOrDefault())
                .FirstOrDefault(parameter => parameter is not null)
            : null;

    private static IEnumerable<IType> ProjectedViews(IType projection) =>
        projection.Members.OfType<MethodMember>()
            .Where(method => method.IsStatic == true && method.Name.StartsWith("Project(", StringComparison.Ordinal))
            .Select(method => method.ReturnType);

    private static string? OwnerOf(string generatedName, HashSet<string> names)
    {
        var name = generatedName;
        while (name.LastIndexOfAny(['+', '/']) is var cut and > 0)
        {
            name = name[..cut];
            if (names.Contains(name))
            {
                return name;
            }
        }

        return null;
    }
}
