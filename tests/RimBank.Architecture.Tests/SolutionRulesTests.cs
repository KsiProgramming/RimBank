namespace RimBank.Architecture.Tests;

// Every rule holds for the real solution. This is the test that fails when a change breaks the architecture.
public sealed class SolutionRulesTests
{
    public static TheoryData<string> RuleIds => [.. ArchitectureRules.All.Select(rule => rule.Id)];

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Rule_holds_for_the_solution(string ruleId)
    {
        var rule = ArchitectureRules.Named(ruleId);

        RuleAssert.Holds(rule, rule.Evaluate(CodeBases.Solution), "solution");
    }

    [Fact]
    public void The_solution_loads_every_project_and_never_the_fixtures()
    {
        CodeBases.SolutionAssemblyNames.ShouldBe(
            [
                "RimBank.AppHost",
                "RimBank.AppHost.Tests",
                "RimBank.Architecture.Tests",
                "RimBank.Host",
                "RimBank.Host.Tests",
                "RimBank.Problems",
                "RimBank.Problems.Tests",
                "RimBank.Postgresql",
                "RimBank.Postgresql.Tests",
                "RimBank.ServiceDefaults",
                "RimBank.SharedKernel",
                "RimBank.SharedKernel.Testing",
                "RimBank.SharedKernel.Tests",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Production_types_and_test_assemblies_are_both_loaded()
    {
        CodeBases.Solution.Types.ShouldContain(type => type.FullName == "RimBank.SharedKernel.Money");
        CodeBases.Solution.TestAssemblies.Select(assembly => assembly.Assembly)
            .ShouldBe(["RimBank.AppHost.Tests", "RimBank.Architecture.Tests", "RimBank.Host.Tests", "RimBank.Postgresql.Tests", "RimBank.Problems.Tests", "RimBank.SharedKernel.Tests"], ignoreOrder: true);
    }
}
