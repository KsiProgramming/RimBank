using Marten;
using Wolverine;

// Bad contexts for the dependency table (tech-stack §4). Each one breaks exactly one rule, named above it.

// shared-kernel-dependencies: SharedKernel is BCL only.
namespace RimBank.SharedKernel.FixtureViolations
{
    public sealed record SharedKernelUsesMarten(IDocumentSession Session);
}

// core-dependencies: a core never depends on Marten (or ASP.NET Core, Wolverine, Npgsql, the MCP SDK).
namespace RimBank.SampleBanking.CoreUsesMarten
{
    public sealed record CoreUsesMartenEntry(IDocumentSession Session);
}

// published-language-dependencies: .PublishedLanguage is BCL only.
namespace RimBank.SampleBanking.PublishedLanguageUsesWolverine.PublishedLanguage
{
    public sealed record PublishedLanguageUsesWolverineOpenedV1(Guid Id, IMessageBus Bus);
}

// subscriptions-dependencies: .Subscriptions never depends on its own .Api, .Mcp or .Postgresql.
namespace RimBank.SampleBanking.SubscriptionsUsesOwnApi.Api
{
    public sealed record SubscriptionsUsesOwnApiRequest(Guid Id);
}

namespace RimBank.SampleBanking.SubscriptionsUsesOwnApi.Subscriptions
{
    public static class SubscriptionsUsesOwnApiSubscription
    {
        public static Guid Translate(RimBank.SampleBanking.SubscriptionsUsesOwnApi.Api.SubscriptionsUsesOwnApiRequest request) =>
            request?.Id ?? Guid.Empty;
    }
}

// api-dependencies: .Api never depends on .Postgresql.
namespace RimBank.SampleBanking.ApiUsesPostgresql.Postgresql
{
    public sealed record ApiUsesPostgresqlDocument(Guid Id);
}

namespace RimBank.SampleBanking.ApiUsesPostgresql.Api
{
    public static class ApiUsesPostgresqlEndpoints
    {
        public static Guid Retrieve(RimBank.SampleBanking.ApiUsesPostgresql.Postgresql.ApiUsesPostgresqlDocument document) =>
            document?.Id ?? Guid.Empty;
    }
}

// mcp-dependencies: .Mcp never depends on .Api.
namespace RimBank.SampleBanking.McpUsesApi.Api
{
    public sealed record McpUsesApiRequest(Guid Id);
}

namespace RimBank.SampleBanking.McpUsesApi.Mcp
{
    public static class McpUsesApiTools
    {
        public static Guid Register(RimBank.SampleBanking.McpUsesApi.Api.McpUsesApiRequest request) => request?.Id ?? Guid.Empty;
    }
}

// postgresql-dependencies: .Postgresql never depends on .Mcp.
namespace RimBank.SampleBanking.PostgresqlUsesMcp.Mcp
{
    public sealed record PostgresqlUsesMcpToolInput(Guid Id);
}

namespace RimBank.SampleBanking.PostgresqlUsesMcp.Postgresql
{
    public static class PostgresqlUsesMcpStore
    {
        public static Guid Save(RimBank.SampleBanking.PostgresqlUsesMcp.Mcp.PostgresqlUsesMcpToolInput input) => input?.Id ?? Guid.Empty;
    }
}

// adapters-dispatch-through-the-message-bus: an .Api dispatches through IMessageBus; configuring Wolverine is the
// host's and the context's .Postgresql's job.
namespace RimBank.SampleBanking.ApiConfiguresWolverine.Api
{
    public static class ApiConfiguresWolverineEndpoints
    {
        public static void Configure(Wolverine.WolverineOptions options) => _ = options;
    }
}

// adapters-dispatch-through-the-message-bus: so does an .Mcp adapter.
namespace RimBank.SampleBanking.McpConfiguresWolverine.Mcp
{
    public static class McpConfiguresWolverineTools
    {
        public static void Configure(Wolverine.WolverineOptions options) => _ = options;
    }
}
