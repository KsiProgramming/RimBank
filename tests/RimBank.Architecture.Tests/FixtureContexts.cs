namespace RimBank.Architecture.Tests;

/// <summary>
/// What each fixture context in <c>RimBank.Architecture.Fixtures</c> is for: the good contexts break no rule, and
/// each bad context breaks exactly the rule it is listed with.
/// </summary>
public static class FixtureContexts
{
    public static IReadOnlyList<string> Good { get; } =
    [
        "RimBank.SampleBanking.SampleLedger",
        "RimBank.SampleBanking.SampleStatement",
    ];

    /// <summary>(bad context namespace, the one rule it breaks).</summary>
    public static IReadOnlyList<(string Context, string RuleId)> Bad { get; } =
    [
        ("RimBank.SharedKernel.FixtureViolations", "shared-kernel-dependencies"),
        ("RimBank.SampleBanking.CoreUsesMarten", "core-dependencies"),
        ("RimBank.SampleBanking.PublishedLanguageUsesWolverine", "published-language-dependencies"),
        ("RimBank.SampleBanking.SubscriptionsUsesOwnApi", "subscriptions-dependencies"),
        ("RimBank.SampleBanking.ApiUsesPostgresql", "api-dependencies"),
        ("RimBank.SampleBanking.McpUsesApi", "mcp-dependencies"),
        ("RimBank.SampleBanking.ApiConfiguresWolverine.Api", "adapters-dispatch-through-the-message-bus"),
        ("RimBank.SampleBanking.McpConfiguresWolverine.Mcp", "adapters-dispatch-through-the-message-bus"),
        ("RimBank.SampleBanking.PostgresqlUsesMcp", "postgresql-dependencies"),
        ("RimBank.SampleBanking.CoreUsesOtherPublishedLanguage", "cross-context-isolation"),
        ("RimBank.SampleBanking.SubscriptionsUsesOtherCore", "cross-context-isolation"),
        ("RimBank.SampleBanking.PublishedLanguageExposesView", "published-language-has-no-views"),
        ("RimBank.SampleBanking.CoreUsesServiceDefaults", "service-defaults-only-in-host"),
        ("RimBank.SampleBanking.PostgresqlUsesAppHost", "nothing-depends-on-apphost"),
        ("RimBank.SampleBanking.ApiImplementsHostModule", "host-is-the-composition-root"),
        ("RimBank.SampleBanking.CoreUsesProblems", "problems-only-in-adapters-and-host"),
        ("RimBank.SampleBanking.PostgresqlUsesProblems", "problems-only-in-adapters-and-host"),
        ("RimBank.Problems.FixtureViolations", "problems-dependencies"),
        ("RimBank.SampleBanking.CoreUsesSharedPostgresql", "shared-postgresql-only-in-postgresql-adapters"),
        ("RimBank.SampleBanking.ApiUsesSharedPostgresql.Api", "shared-postgresql-only-in-postgresql-adapters"),
        ("RimBank.Postgresql.FixtureViolations", "shared-postgresql-dependencies"),
        ("RimBank.SampleBanking.CommandHandlerReadsView", "command-handlers-stay-on-write-side"),
        ("RimBank.SampleBanking.QueryHandlerUsesWritePort", "query-handlers-stay-on-read-side"),
        ("RimBank.SampleBanking.ProjectionWithoutContract", "projections-implement-contract"),
        ("RimBank.SampleBanking.ProjectionUsesTimeProvider", "projections-are-pure"),
        ("RimBank.SampleBanking.AggregateWithoutDecider", "aggregates-implement-decider"),
        ("RimBank.SampleBanking.DeciderAsClass", "aggregates-implement-decider"),
        ("RimBank.UnknownBanking.StrayLedger", "namespace-rule"),
        ("RimBank.SampleBanking.FolderNamedServices", "business-folder-names"),
        ("RimBank.SampleBanking.TypedIdNotReadOnly", "typed-ids-are-readonly-record-structs"),
        ("RimBank.SampleBanking.TypedIdNotRecord", "typed-ids-are-readonly-record-structs"),
        ("RimBank.SampleBanking.FloatingLedger", "no-binary-floating-point"),
    ];

    /// <summary>The fixtures' own service domain catalogue: every SampleBanking context, and not UnknownBanking.</summary>
    public static ServiceDomainCatalogue Catalogue { get; } = new(
        Good.Concat(Bad.Select(bad => bad.Context))
            .Select(ContextAddress.Parse)
            .OfType<ContextAddress>()
            .Where(address => address.BusinessDomain == "SampleBanking")
            .Select(address => (address.BusinessDomain, address.ServiceDomain)));

    public static bool IsIn(Violation violation, string context) =>
        violation.TypeName.StartsWith(context + ".", StringComparison.Ordinal);
}
