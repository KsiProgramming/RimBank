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
                "RimBank.Conformance.Testing",
                "RimBank.Conformance.Tests",
                "RimBank.Core",
                "RimBank.Core.Tests",
                "RimBank.Host",
                "RimBank.Host.Testing",
                "RimBank.Host.Tests",
                "RimBank.Http",
                "RimBank.Http.Tests",
                "RimBank.Mcp",
                "RimBank.Mcp.Tests",
                "RimBank.Party.PartyReferenceDataDirectory.Api",
                "RimBank.Party.PartyReferenceDataDirectory.Api.Tests",
                "RimBank.Party.PartyReferenceDataDirectory.Postgresql",
                "RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests",
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

    // The CQRS rules only bite on what they recognise, so the first real handler and write port must be recognised.
    [Fact]
    public void The_party_register_flow_is_classified_as_write_side()
    {
        var solution = CodeBases.Solution;
        var handler = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Registration.RegisterPartyHandler");
        var command = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Registration.RegisterParty");
        var directory = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.IPartyDirectory");

        solution.IsCommandHandler(handler).ShouldBeTrue();
        solution.IsQueryHandler(handler).ShouldBeFalse();
        solution.IsCommand(command).ShouldBeTrue();
        solution.IsWritePort(directory).ShouldBeTrue();
        solution.IsReadPort(directory).ShouldBeFalse();
    }

    // The same for the read side: the first real query handler, read port, view and projection must be recognised.
    [Fact]
    public void The_party_retrieve_flow_is_classified_as_read_side()
    {
        var solution = CodeBases.Solution;
        var handler = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Retrieval.RetrievePartyReferenceDataDirectoryEntryHandler");
        var query = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Retrieval.RetrievePartyReferenceDataDirectoryEntry");
        var enquiry = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.IPartyDirectoryEnquiry");
        var view = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Retrieval.PartyReferenceDataDirectoryEntryView");
        var projection = solution.Types.Single(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Retrieval.PartyReferenceDataDirectoryEntryViewProjection");

        solution.IsQueryHandler(handler).ShouldBeTrue();
        solution.IsCommandHandler(handler).ShouldBeFalse();
        solution.IsQuery(query).ShouldBeTrue();
        solution.IsCommand(query).ShouldBeFalse();
        solution.IsReadPort(enquiry).ShouldBeTrue();
        solution.IsWritePort(enquiry).ShouldBeFalse();
        solution.IsView(view).ShouldBeTrue();
        solution.IsProjection(projection).ShouldBeTrue();
    }

    // The .Api dependency rules only bite on types they place in a context's .Api part.
    [Fact]
    public void The_party_api_adapter_is_recognised_as_the_party_context_api_part()
    {
        var address = ContextAddress.Parse("RimBank.Party.PartyReferenceDataDirectory.Api.Registration");

        address.ShouldNotBeNull();
        address.Part.ShouldBe(ContextPart.Api);
        CodeBases.Solution.Types.ShouldContain(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Api.Registration.RegisterPartyRequest");
    }

    // The .Postgresql dependency rules only bite on types they place in a context's .Postgresql part.
    [Fact]
    public void The_party_postgresql_adapter_is_recognised_as_the_party_context_postgresql_part()
    {
        var address = ContextAddress.Parse("RimBank.Party.PartyReferenceDataDirectory.Postgresql");

        address.ShouldNotBeNull();
        address.Part.ShouldBe(ContextPart.Postgresql);
        CodeBases.Solution.Types.ShouldContain(type => type.FullName == "RimBank.Party.PartyReferenceDataDirectory.Postgresql.MartenPartyDirectory");
    }

    [Fact]
    public void Production_types_and_test_assemblies_are_both_loaded()
    {
        CodeBases.Solution.Types.ShouldContain(type => type.FullName == "RimBank.SharedKernel.Money");
        CodeBases.Solution.TestAssemblies.Select(assembly => assembly.Assembly)
            .ShouldBe(["RimBank.AppHost.Tests", "RimBank.Architecture.Tests", "RimBank.Conformance.Tests", "RimBank.Core.Tests", "RimBank.Host.Tests", "RimBank.Http.Tests", "RimBank.Mcp.Tests", "RimBank.Party.PartyReferenceDataDirectory.Api.Tests", "RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests", "RimBank.Postgresql.Tests", "RimBank.Problems.Tests", "RimBank.SharedKernel.Tests"], ignoreOrder: true);
    }
}
