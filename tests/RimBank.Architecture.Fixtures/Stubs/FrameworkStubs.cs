// Stand-ins for the framework packages the dependency rules talk about (tech-stack §4).
// The architecture rules match dependencies by namespace, so these empty types are enough to break a rule
// on purpose without referencing Marten, Wolverine, Npgsql, ASP.NET Core or the MCP SDK.
#pragma warning disable CA1040 // Empty interfaces are the point of a stub.
#pragma warning disable IDE0130 // The namespaces must match the real packages, not the folder.

namespace Marten
{
    public interface IDocumentSession;
}

namespace Wolverine
{
    public interface IMessageBus;
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

namespace ModelContextProtocol.Server
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class McpServerToolAttribute : System.Attribute;
}
