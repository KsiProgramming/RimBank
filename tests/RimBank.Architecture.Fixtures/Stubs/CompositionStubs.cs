// Stand-ins for the composition projects (ADR 0014). The rules match dependencies by namespace, so these empty
// types let a bad context depend on RimBank.Host, RimBank.AppHost, RimBank.ServiceDefaults or RimBank.Problems without
// referencing them.

namespace RimBank.Host.Modules
{
    public interface IServiceDomainModule
    {
        string ServiceDomain { get; }
    }
}

namespace RimBank.AppHost
{
    public static class AppHostResources
    {
        // A property, not a const: a const is inlined and leaves no dependency for the rule to see.
        public static string Database => "rimbank";
    }
}

namespace RimBank.ServiceDefaults
{
    public static class ServiceDefaultsExtensions
    {
        public static string LiveTag => "live";
    }
}

namespace RimBank.Problems
{
    public static class SharedProblems
    {
        public static string NotFoundCode => "shared.not-found";
    }
}

namespace RimBank.Api
{
    public static class IdempotencyKeyHeader
    {
        public static string HeaderName => "Idempotency-Key";
    }
}

namespace RimBank.Mcp.Errors
{
    public static class ToolErrors
    {
        public static string ArgumentsInvalidCode => "shared.tool-arguments-invalid";
    }
}

namespace RimBank.Postgresql
{
    public static class ServiceDomainStoreRegistration
    {
        public static string ReadyTag => "ready";
    }
}
