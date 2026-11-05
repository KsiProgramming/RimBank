namespace RimBank.Architecture.Tests;

// Proves every rule bites: it passes on the good fixture contexts and fails on its bad one, and every bad context
// breaks exactly one rule (violation isolation). Without this, a rule could pass on the solution only because
// there is nothing yet for it to check.
public sealed class FixtureRulesTests
{
    public static TheoryData<string> FixtureRuleIds => [.. ArchitectureRules.All.Where(rule => !rule.SolutionOnly).Select(rule => rule.Id)];

    public static TheoryData<string> BadContexts => [.. FixtureContexts.Bad.Select(bad => bad.Context)];

    [Theory]
    [MemberData(nameof(FixtureRuleIds))]
    public void Rule_holds_for_the_good_contexts(string ruleId)
    {
        var rule = ArchitectureRules.Named(ruleId);

        var violations = rule.Evaluate(CodeBases.Fixtures)
            .Where(violation => FixtureContexts.Good.Any(context => FixtureContexts.IsIn(violation, context)))
            .ToList();

        RuleAssert.Holds(rule, violations, "good fixture contexts");
    }

    [Theory]
    [MemberData(nameof(BadContexts))]
    public void Bad_context_breaks_its_rule(string context)
    {
        var rule = ArchitectureRules.Named(RuleOf(context));

        var violations = rule.Evaluate(CodeBases.Fixtures).Where(violation => FixtureContexts.IsIn(violation, context)).ToList();

        violations.ShouldNotBeEmpty($"'{context}' should break '{rule.Id}'");
        RuleAssert.Describe(rule, violations, "fixtures").ShouldContain(rule.Because);
    }

    [Theory]
    [MemberData(nameof(BadContexts))]
    public void Bad_context_breaks_no_other_rule(string context)
    {
        var expected = RuleOf(context);

        foreach (var rule in ArchitectureRules.All.Where(rule => !rule.SolutionOnly && rule.Id != expected))
        {
            var violations = rule.Evaluate(CodeBases.Fixtures).Where(violation => FixtureContexts.IsIn(violation, context)).ToList();
            RuleAssert.Holds(rule, violations, $"bad fixture context '{context}' (it should break '{expected}' only)");
        }
    }

    [Theory]
    [MemberData(nameof(FixtureRuleIds))]
    public void Rule_has_a_bad_context_and_fires_nowhere_else(string ruleId)
    {
        var rule = ArchitectureRules.Named(ruleId);
        var contexts = FixtureContexts.Bad.Where(bad => bad.RuleId == ruleId).Select(bad => bad.Context).ToList();

        contexts.ShouldNotBeEmpty($"'{ruleId}' has no bad fixture context, so nothing proves it bites");
        var stray = rule.Evaluate(CodeBases.Fixtures)
            .Where(violation => !contexts.Any(context => FixtureContexts.IsIn(violation, context)))
            .ToList();
        RuleAssert.Holds(rule, stray, "fixtures outside its bad contexts");
    }

    [Fact]
    public void A_failure_names_the_type_the_dependency_and_the_source()
    {
        var rule = ArchitectureRules.CoreDependencies;

        var message = RuleAssert.Describe(rule, rule.Evaluate(CodeBases.Fixtures), "fixtures");

        message.ShouldContain("RimBank.SampleBanking.CoreUsesMarten.CoreUsesMartenEntry");
        message.ShouldContain("Marten.IDocumentSession");
        message.ShouldContain("tech-stack §4");
    }

    private static string RuleOf(string context) => FixtureContexts.Bad.Single(bad => bad.Context == context).RuleId;
}
