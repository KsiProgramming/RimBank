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
