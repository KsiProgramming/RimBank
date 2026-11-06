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
    [InlineData("RimBank.Http")]
    [InlineData("RimBank.Mcp")]
    [InlineData("RimBank.Postgresql")]
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
    [InlineData("RimBank.Party.PartyReferenceDataDirectory")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Core")]
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
    public void The_core_project_holding_catalogued_core_namespaces_passes()
    {
        ArchitectureRules.AssemblyNameProblems(
                "RimBank.Core",
                ["RimBank.Party.PartyReferenceDataDirectory", "RimBank.Party.PartyReferenceDataDirectory.Registration", "RimBank.LoansAndDeposits.CurrentAccount"],
                _catalogue)
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Api")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage")]
    [InlineData("RimBank.SharedKernel")]
    [InlineData("RimBank.Core")]
    [InlineData("RimBank.Core.Party")]
    [InlineData("RimBank.Customers.Directory")]
    [InlineData("Banking.Party")]
    public void The_core_project_holding_anything_but_a_catalogued_core_fails(string namespaceName)
    {
        ArchitectureRules.AssemblyNameProblems("RimBank.Core", ["RimBank.Party.PartyReferenceDataDirectory", namespaceName], _catalogue)
            .ShouldHaveSingleItem().Detail.ShouldContain($"'{namespaceName}'");
    }

    [Fact]
    public void Core_types_in_the_core_project_and_other_types_anywhere_pass()
    {
        ArchitectureRules.CoreTypeProblems(
                [
                    ("RimBank.Party.PartyReferenceDataDirectory.RegisterParty", "RimBank.Party.PartyReferenceDataDirectory.Registration", "RimBank.Core"),
                    ("RimBank.Party.PartyReferenceDataDirectory.Api.RegisterEndpoint", "RimBank.Party.PartyReferenceDataDirectory.Api", "RimBank.Party.PartyReferenceDataDirectory.Api"),
                    ("RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage.PartyCreated", "RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage", "RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage"),
                    ("RimBank.SharedKernel.Money", "RimBank.SharedKernel", "RimBank.SharedKernel"),
                    ("RimBank.Unknown.Context.Stray", "RimBank.Unknown.Context", "RimBank.Unknown.Context"),
                ],
                _catalogue)
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory.Api")]
    [InlineData("RimBank.Party.PartyReferenceDataDirectory")]
    [InlineData("RimBank.Host")]
    public void A_core_type_outside_the_core_project_fails(string assemblyName)
    {
        ArchitectureRules.CoreTypeProblems(
                [("RimBank.Party.PartyReferenceDataDirectory.Registration.RegisterParty", "RimBank.Party.PartyReferenceDataDirectory.Registration", assemblyName)],
                _catalogue)
            .ShouldHaveSingleItem().Detail.ShouldContain($"lives in {assemblyName}, not RimBank.Core");
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
        ArchitectureRules.NamespaceIsAllowed("RimBank.Postgresql", _catalogue).ShouldBeTrue();
        ArchitectureRules.NamespaceIsAllowed("RimBank.Party.PartyReferenceDataDirectory.Registration", _catalogue).ShouldBeTrue();
        ArchitectureRules.NamespaceIsAllowed("RimBank.Party.Directory", _catalogue).ShouldBeFalse();
        ArchitectureRules.NamespaceIsAllowed("Banking.Party", _catalogue).ShouldBeFalse();
    }

    // The fixtures stub Wolverine, so the real interface hierarchy is checked here: a Wolverine upgrade that moves
    // InvokeAsync to another interface fails this test instead of every .Api and .Mcp adapter.
    [Fact]
    public void The_message_bus_surface_is_what_IMessageBus_is_made_of()
    {
        // By assembly-qualified name: the fixtures' stub has the same full name.
        var messageBus = Type.GetType("Wolverine.IMessageBus, Wolverine", throwOnError: true)!;

        ArchitectureRules.MessageBusSurface.ShouldBe(
            [messageBus.FullName!, .. messageBus.GetInterfaces().Select(type => type.FullName!)],
            ignoreOrder: true);
    }
}
