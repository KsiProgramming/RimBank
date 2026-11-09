using System.ComponentModel;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace RimBank.Host.Tests;

/// <summary>
/// A test-only module that registers MCP tools the way a context's <c>.Mcp</c> adapter will, through
/// <see cref="IServiceDomainModule.AddMcpTools"/> (ADR 0021). It maps no HTTP endpoint.
/// </summary>
internal sealed class McpServiceDomainModule(string serviceDomain = "FakeServiceDomain") : IServiceDomainModule
{
    public string ServiceDomain => serviceDomain;

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void AddOperationPolicies(OperationPolicies policies)
    {
    }

    public void AddMcpTools(IMcpServerBuilder tools) => tools.WithTools<FakeServiceDomainTools>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

/// <summary>The smoke tool of roadmap 0.15: a Retrieve-style, read-only tool that answers with a fixed text.</summary>
[McpServerToolType]
internal sealed class FakeServiceDomainTools
{
    public const string RetrieveName = "fake_service_domain_retrieve";

    public const string RetrieveDescription = "Retrieves the fake service domain's fixed reference text.";

    public const string RetrieveResult = "fake-service-domain-entry";

    [McpServerTool(Name = RetrieveName, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description(RetrieveDescription)]
    public static string Retrieve() => RetrieveResult;
}
