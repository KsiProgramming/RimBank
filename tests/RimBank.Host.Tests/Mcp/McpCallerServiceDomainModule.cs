using System.ComponentModel;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RimBank.Host.Modules;
using RimBank.Host.Security.Tests;
using Wolverine;

namespace RimBank.Host.Mcp.Tests;

/// <summary>
/// A test-only module whose tool dispatches through the message bus, as a context's <c>.Mcp</c> adapter will, and
/// answers with the user name its handler saw on the envelope (ADR 0019, ADR 0022).
/// </summary>
internal sealed class McpCallerServiceDomainModule : IServiceDomainModule
{
    public string ServiceDomain => "FakeCallerServiceDomain";

    public void AddServices(IHostApplicationBuilder builder) =>
        builder.Services.ConfigureWolverine(options => options.Discovery.IncludeType(typeof(WhoSentThisHandler)));

    public void AddMcpTools(IMcpServerBuilder tools) => tools.WithTools<FakeCallerServiceDomainTools>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

[McpServerToolType]
internal sealed class FakeCallerServiceDomainTools
{
    public const string ExecuteName = "fake_caller_service_domain_execute";

    [McpServerTool(Name = ExecuteName, ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Sends a message through the bus and answers with the caller its handler saw.")]
    public static Task<string> Execute(IMessageBus bus, CancellationToken cancellationToken) =>
        bus.InvokeAsync<string>(new WhoSentThis(), cancellationToken);
}
