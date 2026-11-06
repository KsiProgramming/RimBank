// Stand-ins for the framework packages the dependency rules talk about (tech-stack §4).
// The architecture rules match dependencies by namespace, so these empty types are enough to break a rule
// on purpose without referencing Marten, Wolverine, Npgsql, ASP.NET Core or the MCP SDK.

namespace Marten
{
    public interface IDocumentSession;
}

namespace Wolverine
{
    public interface IMessageBus;

    public sealed class WolverineOptions;
}

namespace Wolverine.Marten
{
    public interface IMartenOutbox;
}

namespace Npgsql
{
    public interface INpgsqlConnection;
}

namespace Microsoft.AspNetCore.Http
{
    public interface IResult;
}

namespace Microsoft.AspNetCore.Authorization
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
    public sealed class AllowAnonymousAttribute : System.Attribute;
}

namespace Microsoft.AspNetCore.Builder
{
    public static class AuthorizationEndpointConventionBuilderExtensions
    {
        public static TBuilder AllowAnonymous<TBuilder>(this TBuilder builder) => builder;

        public static TBuilder RequireAuthorization<TBuilder>(this TBuilder builder) => builder;
    }
}

namespace ModelContextProtocol.Server
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class McpServerToolAttribute : System.Attribute;
}
