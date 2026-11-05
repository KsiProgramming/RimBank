using System.Reflection;
using ArchUnitNET.Loader;

namespace RimBank.Architecture.Tests;

/// <summary>The two code bases the rules run on: the real solution and the fixtures. Each is loaded once.</summary>
public static class CodeBases
{
    private const string FixturesAssembly = "RimBank.Architecture.Fixtures";

    private static readonly Lazy<CodeBase> _solution = new(LoadSolution);
    private static readonly Lazy<CodeBase> _fixtures = new(LoadFixtures);

    /// <summary>
    /// Every <c>RimBank.*</c> assembly in this test project's output, except the fixtures. A project gets there by a
    /// <c>ProjectReference</c> from <c>RimBank.Architecture.Tests.csproj</c>, which every new project adds
    /// (CONTRIBUTING, "Architecture rules").
    /// </summary>
    public static CodeBase Solution => _solution.Value;

    /// <summary>The fixture contexts (good and bad), with SharedKernel loaded so the markers resolve.</summary>
    public static CodeBase Fixtures => _fixtures.Value;

    /// <summary>The names of the assemblies in <see cref="Solution"/>.</summary>
    public static IReadOnlyList<string> SolutionAssemblyNames { get; } =
    [
        .. Directory.GetFiles(AppContext.BaseDirectory, "RimBank.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => name != FixturesAssembly)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary><c>*.Tests</c> assemblies hold tests; <c>*.Testing</c> and the fixtures are shared test libraries.</summary>
    public static bool IsTestAssembly(string name) => name.EndsWith(".Tests", StringComparison.Ordinal);

    public static bool IsProductionAssembly(string name) =>
        !IsTestAssembly(name) && !name.EndsWith(".Testing", StringComparison.Ordinal) && name != FixturesAssembly;

    private static CodeBase LoadSolution()
    {
        var assemblies = SolutionAssemblyNames.Select(name => Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, name + ".dll"))).ToArray();
        var architecture = new ArchLoader().LoadAssemblies(assemblies).Build();
        var types = architecture.Types.Where(type => !IsGenerated(type)).ToList();
        return new CodeBase(
            "solution",
            types.Where(type => IsProductionAssembly(AssemblyName(type))),
            types.Where(type => IsTestAssembly(AssemblyName(type)))
                .GroupBy(AssemblyName, StringComparer.Ordinal)
                .Select(group => (group.Key, (IReadOnlyList<ArchUnitNET.Domain.IType>)[.. group])),
            ServiceDomainCatalogue.Solution);
    }

    private static CodeBase LoadFixtures()
    {
        var architecture = new ArchLoader()
            .LoadAssemblies(typeof(SharedKernel.IDecider<,,>).Assembly, Assembly.Load(FixturesAssembly))
            .Build();
        return new CodeBase(
            "fixtures",
            architecture.Types.Where(type => AssemblyName(type) == FixturesAssembly && Namespaces.IsWithin(type.NamespaceName(), "RimBank")),
            [],
            FixtureContexts.Catalogue);
    }

    private static string AssemblyName(ArchUnitNET.Domain.IType type) => type.Assembly.Name.Split(',')[0];

    /// <summary>
    /// The rules govern hand-written code. Types a tool emits with <c>[GeneratedCode]</c>, such as Aspire's
    /// <c>Projects.RimBank_Host</c> metadata in <c>RimBank.AppHost</c>, are not ours to name (ADR 0014).
    /// </summary>
    private static bool IsGenerated(ArchUnitNET.Domain.IType type) =>
        type.Attributes.Any(attribute => attribute.FullName == "System.CodeDom.Compiler.GeneratedCodeAttribute");
}
