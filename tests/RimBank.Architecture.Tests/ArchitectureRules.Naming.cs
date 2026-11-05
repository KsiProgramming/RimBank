using ArchUnitNET.Domain;

namespace RimBank.Architecture.Tests;

// The naming rules (tech-stack §3, ADR 0011) and the value rules (ADR 0008, ADR 0009).
public static partial class ArchitectureRules
{
    /// <summary>Technical folder names a business-first codebase never uses (mission principle 1, tech-stack §3.4).</summary>
    public static IReadOnlyList<string> ForbiddenFolderNames { get; } =
        ["Services", "Repositories", "Helpers", "Managers", "Utils", "Dtos", "Commands", "Queries"];

    public static ArchitectureRule NamespaceRule { get; } = new(
        "namespace-rule",
        "tech-stack §3.1–3.2 and ADR 0011: RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>], with the service domain in the catalogue",
        code => code.Types
            .Where(type => !NamespaceIsAllowed(type.NamespaceName(), code.Catalogue))
            .Select(type => new Violation(type.Display(), $"namespace '{type.NamespaceName()}' is not a composition project or a catalogued RimBank.<BusinessDomain>.<ServiceDomain>")));

    public static ArchitectureRule AssemblyNames { get; } = new(
        "assembly-names",
        "tech-stack §3.1 and ADR 0011: a project is named after its root namespace, with an allowed adapter suffix",
        code => code.Types
            .GroupBy(type => type.Assembly.Name.Split(',')[0], StringComparer.Ordinal)
            .SelectMany(assembly => AssemblyNameProblems(assembly.Key, assembly.Select(type => type.NamespaceName()), code.Catalogue)),
        SolutionOnly: true);

    public static ArchitectureRule BusinessFolderNames { get; } = new(
        "business-folder-names",
        "mission principle 1 and tech-stack §3.4: folders follow aggregates, Behavior Qualifiers and flows, never Services, Repositories, Helpers, Managers, Utils, Dtos, Commands or Queries",
        code => code.Types
            .Where(type => type.NamespaceName().Split('.').Any(segment => ForbiddenFolderNames.Contains(segment)))
            .Select(type => new Violation(type.Display(), $"namespace '{type.NamespaceName()}' uses a technical folder name")));

    public static ArchitectureRule TypedIdsAreReadonlyRecordStructs { get; } = new(
        "typed-ids-are-readonly-record-structs",
        "tech-stack §7 and ADR 0008: every ITypedId<T> is a readonly record struct",
        code => code.Types
            .Where(code.IsTypedId)
            .Where(type => !IsReadonlyRecordStruct(type))
            .Select(type => new Violation(type.Display(), "implements ITypedId but is not a readonly record struct")));

    public static ArchitectureRule NoBinaryFloatingPoint { get; } = new(
        "no-binary-floating-point",
        "mission quality bar and ADR 0009: no double or float in cores, SharedKernel or .PublishedLanguage",
        code => code.Types
            .Where(type => Namespaces.IsWithin(type.NamespaceName(), "RimBank.SharedKernel")
                || ContextAddress.Parse(type.NamespaceName())?.Part is ContextPart.Core or ContextPart.PublishedLanguage)
            .SelectMany(type => FloatingPointMembers(type).Select(member => new Violation(type.Display(), $"{member} uses double or float"))));

    public static ArchitectureRule TestNamespaces { get; } = new(
        "test-namespaces",
        "tech-stack §3.3: a test namespace is <namespace under test>.<folder>.Tests and always ends with .Tests",
        code => code.TestAssemblies.SelectMany(assembly => TestNamespaceProblems(assembly.Types.Select(type => (type.Display(), type.NamespaceName())))),
        SolutionOnly: true);

    public static bool NamespaceIsAllowed(string namespaceName, ServiceDomainCatalogue catalogue) =>
        ContextAddress.IsCompositionProject(namespaceName)
        || ContextAddress.Parse(namespaceName) is { } address && catalogue.Contains(address.BusinessDomain, address.ServiceDomain);

    /// <summary>The assembly name rule as a pure function, so its negative cases can be tested without an assembly.</summary>
    public static IEnumerable<Violation> AssemblyNameProblems(string assemblyName, IEnumerable<string> namespaces, ServiceDomainCatalogue catalogue)
    {
        var segments = assemblyName.Split('.');
        var named = ContextAddress.CompositionProjects.Contains(assemblyName)
            || segments.Length is 3 or 4
                && segments[0] == "RimBank"
                && catalogue.Contains(segments[1], segments[2])
                && (segments.Length == 3 || ContextAddress.AdapterSuffixes.ContainsKey(segments[3]));
        if (!named)
        {
            yield return new Violation(assemblyName, "is not a composition project or RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>] with a catalogued service domain and an allowed adapter suffix");
        }

        foreach (var namespaceName in namespaces.Distinct().Where(namespaceName => !Namespaces.IsWithin(namespaceName, assemblyName)))
        {
            yield return new Violation(assemblyName, $"holds the namespace '{namespaceName}', outside its root namespace");
        }
    }

    /// <summary>The test namespace rule as a pure function over (type, namespace) pairs.</summary>
    public static IEnumerable<Violation> TestNamespaceProblems(IEnumerable<(string TypeName, string NamespaceName)> types) =>
        types
            .Where(type => !type.NamespaceName.EndsWith(".Tests", StringComparison.Ordinal))
            .Select(type => new Violation(type.TypeName, $"namespace '{type.NamespaceName}' does not end with .Tests"));

    // ArchUnitNET exposes neither "readonly" nor "record" on a struct, so the rule reads what the compiler emits:
    // [IsReadOnly] on a readonly struct, and a [CompilerGenerated] PrintMembers on a record struct.
    private static bool IsReadonlyRecordStruct(IType type) =>
        type is Struct
        && type.Attributes.Any(attribute => attribute.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute")
        && type.Members.OfType<MethodMember>().Any(method =>
            method.Name.StartsWith("PrintMembers(", StringComparison.Ordinal)
            && method.Attributes.Any(attribute => attribute.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"));

    private static IEnumerable<string> FloatingPointMembers(IType type)
    {
        foreach (var field in type.Members.OfType<FieldMember>().Where(field => !field.IsCompilerGenerated))
        {
            if (IsFloatingPoint(field.Type, field.GenericArguments))
            {
                yield return $"field {field.Name}";
            }
        }

        foreach (var property in type.Members.OfType<PropertyMember>())
        {
            if (IsFloatingPoint(property.Type, property.GenericArguments))
            {
                yield return $"property {property.Name}";
            }
        }

        foreach (var method in type.Members.OfType<MethodMember>().Where(method => !method.IsCompilerGenerated))
        {
            if (IsFloatingPoint(method.ReturnTypeInstance.Type, method.ReturnTypeInstance.GenericArguments)
                || method.ParameterInstances.Any(parameter => IsFloatingPoint(parameter.Type, parameter.GenericArguments)))
            {
                yield return $"method {method.Name}";
            }
        }
    }

    private static bool IsFloatingPoint(IType type, IEnumerable<GenericArgument> genericArguments) =>
        type.FullName is "System.Double" or "System.Single"
        || genericArguments.Any(argument => IsFloatingPoint(argument.Type, argument.GenericArguments));
}
