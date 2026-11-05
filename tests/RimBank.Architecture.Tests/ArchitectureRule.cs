namespace RimBank.Architecture.Tests;

/// <summary>One broken rule on one type, in plain words.</summary>
public sealed record Violation(string TypeName, string Detail)
{
    public override string ToString() => $"{TypeName}: {Detail}";
}

/// <summary>
/// An architecture rule: what it checks, why (the constitution section or ADR it comes from), and the check itself.
/// </summary>
/// <param name="Id">Stable kebab-case identifier, used in test names and the CONTRIBUTING catalogue.</param>
/// <param name="Because">The source of the rule, shown in every failure message.</param>
/// <param name="Check">Finds the violations in a code base.</param>
/// <param name="SolutionOnly">
/// The rule reads assembly names or test assemblies, which the single fixture assembly cannot show; its negative
/// cases are in-memory tests instead.
/// </param>
public sealed record ArchitectureRule(string Id, string Because, Func<CodeBase, IEnumerable<Violation>> Check, bool SolutionOnly = false)
{
    public IReadOnlyList<Violation> Evaluate(CodeBase code) => [.. Check(code).Distinct()];

    public override string ToString() => Id;
}
