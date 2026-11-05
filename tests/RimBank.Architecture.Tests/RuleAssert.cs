namespace RimBank.Architecture.Tests;

internal static class RuleAssert
{
    /// <summary>Fails with the rule, its source and every offending type when <paramref name="violations"/> is not empty.</summary>
    public static void Holds(ArchitectureRule rule, IReadOnlyCollection<Violation> violations, string where)
    {
        if (violations.Count > 0)
        {
            throw new ShouldAssertException(Describe(rule, violations, where));
        }
    }

    public static string Describe(ArchitectureRule rule, IEnumerable<Violation> violations, string where) =>
        $"Architecture rule '{rule.Id}' is broken in the {where}.{Environment.NewLine}"
        + $"Because: {rule.Because}.{Environment.NewLine}"
        + string.Join(Environment.NewLine, violations.Select(violation => $"  - {violation}"));
}
