// Bad contexts for the composition projects (ADR 0014). Each one breaks exactly one rule, named above it.

// service-defaults-only-in-host: a core never applies the service defaults itself.
namespace RimBank.SampleBanking.CoreUsesServiceDefaults
{
    public static class CoreUsesServiceDefaultsHealth
    {
        public static string Tag() => RimBank.ServiceDefaults.ServiceDefaultsExtensions.LiveTag;
    }
}

// nothing-depends-on-apphost: an adapter never reads the AppHost's resource names.
namespace RimBank.SampleBanking.PostgresqlUsesAppHost.Postgresql
{
    public static class PostgresqlUsesAppHostStore
    {
        public static string ConnectionStringName() => RimBank.AppHost.AppHostResources.Database;
    }
}

// host-is-the-composition-root: a context's .Api never implements the host's module contract; the module class
// lives in RimBank.Host/Modules/<BusinessDomain>/ and calls the adapter's extension methods.
namespace RimBank.SampleBanking.ApiImplementsHostModule.Api
{
    public sealed class ApiImplementsHostModuleModule : RimBank.Host.Modules.IServiceDomainModule
    {
        public string ServiceDomain => "ApiImplementsHostModule";
    }
}

// problems-only-in-adapters-and-host: a core returns an Error; it never builds Problem Details itself.
namespace RimBank.SampleBanking.CoreUsesProblems
{
    public static class CoreUsesProblemsEntry
    {
        public static string NotFound() => RimBank.Problems.SharedProblems.NotFoundCode;
    }
}

// problems-only-in-adapters-and-host: a .Postgresql adapter never answers a caller, so it has no Problem Details.
namespace RimBank.SampleBanking.PostgresqlUsesProblems.Postgresql
{
    public static class PostgresqlUsesProblemsStore
    {
        public static string NotFound() => RimBank.Problems.SharedProblems.NotFoundCode;
    }
}

// problems-dependencies: RimBank.Problems references SharedKernel and ASP.NET Core only, never Marten.
namespace RimBank.Problems.FixtureViolations
{
    public sealed record ProblemsUsesMarten(Marten.IDocumentSession Session);
}

// http-only-in-api-and-host: a core receives the idempotency key on its command; it never reads HTTP headers.
namespace RimBank.SampleBanking.CoreUsesHttp
{
    public static class CoreUsesHttpEntry
    {
        public static string Header() => RimBank.Http.IdempotencyKeyHeader.HeaderName;
    }
}

// http-only-in-api-and-host: an .Mcp adapter takes the key as a tool argument, never through the HTTP conventions.
namespace RimBank.SampleBanking.McpUsesHttp.Mcp
{
    public static class McpUsesHttpTools
    {
        public static string Header() => RimBank.Http.IdempotencyKeyHeader.HeaderName;
    }
}

// http-dependencies: RimBank.Http reads requests; it never touches a store.
namespace RimBank.Http.FixtureViolations
{
    public sealed record HttpUsesMarten(Marten.IDocumentSession Session);
}

// shared-postgresql-only-in-postgresql-adapters: a core reaches its store through its ports, never through the
// Marten conventions.
namespace RimBank.SampleBanking.CoreUsesSharedPostgresql
{
    public static class CoreUsesSharedPostgresqlEntry
    {
        public static string HealthTag() => RimBank.Postgresql.ServiceDomainStoreRegistration.ReadyTag;
    }
}

// shared-postgresql-only-in-postgresql-adapters: an .Api never touches the store; it dispatches commands and queries.
namespace RimBank.SampleBanking.ApiUsesSharedPostgresql.Api
{
    public static class ApiUsesSharedPostgresqlEndpoints
    {
        public static string HealthTag() => RimBank.Postgresql.ServiceDomainStoreRegistration.ReadyTag;
    }
}

// shared-postgresql-dependencies: RimBank.Postgresql never answers an HTTP caller, so it never uses ASP.NET Core.
namespace RimBank.Postgresql.FixtureViolations
{
    public sealed record SharedPostgresqlUsesAspNetCore(Microsoft.AspNetCore.Http.IResult Result);
}
