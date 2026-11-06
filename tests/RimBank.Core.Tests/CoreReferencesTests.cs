using System.Reflection;

namespace RimBank.Core.Tests;

// RimBank.Core is the core of every context: domain and application logic only (ADR 0023, tech-stack §4).
public sealed class CoreReferencesTests
{
    [Fact]
    public void The_core_references_only_the_shared_kernel_published_languages_and_the_bcl()
    {
        var core = Assembly.Load(new AssemblyName("RimBank.Core"));

        core.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsAllowed(name))
            .ShouldBeEmpty();
    }

    private static bool IsAllowed(string name) =>
        name is "RimBank.SharedKernel" or "netstandard" or "mscorlib"
        || name.StartsWith("System.", StringComparison.Ordinal) || name == "System"
        || name.StartsWith("RimBank.", StringComparison.Ordinal) && name.EndsWith(".PublishedLanguage", StringComparison.Ordinal);
}
