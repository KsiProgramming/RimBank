namespace RimBank.Architecture.Tests;

/// <summary>
/// Every architecture rule of the constitution (tech-stack §2, §3, §4, §8.4, §10; ADR 0008–0012, ADR 0014–0019, ADR 0022, ADR 0023), in the order of the
/// CONTRIBUTING catalogue.
/// </summary>
public static partial class ArchitectureRules
{
    // An expression body, not an initialiser: the rules are initialised in other files of this partial class, and
    // static initialisers across partial files run in file order.
    public static IReadOnlyList<ArchitectureRule> All =>
    [
        SharedKernelDependencies,
        CoreDependencies,
        PublishedLanguageDependencies,
        SubscriptionsDependencies,
        ApiDependencies,
        McpDependencies,
        AdaptersDispatchThroughTheMessageBus,
        PostgresqlDependencies,
        CrossContextIsolation,
        PublishedLanguageHasNoViews,
        ServiceDefaultsOnlyInHost,
        NothingDependsOnAppHost,
        HostIsTheCompositionRoot,
        ProblemsOnlyInAdaptersAndHost,
        ProblemsDependencies,
        HttpOnlyInApiAndHost,
        HttpDependencies,
        SharedMcpOnlyInMcpAndHost,
        SharedMcpDependencies,
        AllowAnonymousOnlyInServiceDefaults,
        SharedPostgresqlOnlyInPostgresqlAdapters,
        SharedPostgresqlDependencies,
        CommandHandlersStayOnWriteSide,
        QueryHandlersStayOnReadSide,
        ProjectionsImplementContract,
        ProjectionsArePure,
        AggregatesImplementDecider,
        NamespaceRule,
        AssemblyNames,
        CoresLiveInRimBankCore,
        HttpAndMcpAdaptersLiveInSharedProjects,
        BusinessFolderNames,
        TypedIdsAreReadonlyRecordStructs,
        NoBinaryFloatingPoint,
        TestNamespaces,
    ];

    public static ArchitectureRule Named(string id) => All.Single(rule => rule.Id == id);
}
