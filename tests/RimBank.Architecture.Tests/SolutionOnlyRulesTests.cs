namespace RimBank.Architecture.Tests;

// The rules that read assembly names and test assemblies cannot fail on the single fixture assembly, so their
// negative cases are checked here on plain names.
public sealed class SolutionOnlyRulesTests
{
    private static readonly ServiceDomainCatalogue _catalogue = ServiceDomainCatalogue.Solution;

    [Theory]
    [InlineData("RimBank.SharedKernel")]
    [InlineData("RimBank.Host")]
    [InlineData("RimBank.Problems")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Api")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Mcp")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Postgresql")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage")]
    [InlineData("RimBank.LoansAndDeposits.CurrentAccount.Subscriptions")]
    public void Allowed_project_names_pass(string assemblyName)
    {
        ArchitectureRules.AssemblyNameProblems(assemblyName, [assemblyName], _catalogue).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Infrastructure")]
    [InlineData("RimBank.Party.PartyDirectory")]
    [InlineData("RimBank.Customers.PartyReferenceDataDirectory")]
    [InlineData("RimBank.Party")]
    [InlineData("PartyReferenceDataDirectory")]
    public void Project_names_outside_the_rule_fail(string assemblyName)
    {
        ArchitectureRules.AssemblyNameProblems(assemblyName, [assemblyName], _catalogue)
            .ShouldHaveSingleItem().Detail.ShouldContain("allowed adapter suffix");
    }

    [Fact]
    public void A_namespace_outside_the_project_root_fails()
    {
        ArchitectureRules.AssemblyNameProblems(
                "RimBank.Party.PartyReferenceDataDirectory.Api",
                ["RimBank.Party.PartyReferenceDataDirectory.Api.Registration", "RimBank.Party.PartyReferenceDataDirectory.Registration"],
                _catalogue)
            .ShouldHaveSingleItem().Detail.ShouldContain("'RimBank.Party.PartyReferenceDataDirectory.Registration'");
    }

    [Fact]
    public void Test_namespaces_ending_with_Tests_pass()
    {
        ArchitectureRules.TestNamespaceProblems(
            [
                ("RimBank.SharedKernel.Tests.ErrorTests", "RimBank.SharedKernel.Tests"),
                ("RimBank.SharedKernel.MoneyAndCurrency.Tests.MoneyTests", "RimBank.SharedKernel.MoneyAndCurrency.Tests"),
            ]).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RimBank.SharedKernel.Tests.MoneyAndCurrency")]
    [InlineData("RimBank.SharedKernel")]
    [InlineData("RimBank.SharedKernel.TestsHelpers")]
    public void Test_namespaces_not_ending_with_Tests_fail(string namespaceName)
    {
        ArchitectureRules.TestNamespaceProblems([($"{namespaceName}.SomeTests", namespaceName)])
            .ShouldHaveSingleItem().Detail.ShouldContain("does not end with .Tests");
    }

    [Fact]
    public void Namespace_rule_accepts_composition_projects_and_catalogued_contexts_only()
    {
        ArchitectureRules.NamespaceIsAllowed("RimBank.SharedKernel", _catalogue).ShouldBeTrue();
        ArchitectureRules.NamespaceIsAllowed("RimBank.Problems", _catalogue).ShouldBeTrue();
        ArchitectureRules.NamespaceIsAllowed("RimBank.Party.PartyReferenceDataDirectory.Registration", _catalogue).ShouldBeTrue();
        ArchitectureRules.NamespaceIsAllowed("RimBank.Party.Directory", _catalogue).ShouldBeFalse();
        ArchitectureRules.NamespaceIsAllowed("Banking.Party", _catalogue).ShouldBeFalse();
    }
}
