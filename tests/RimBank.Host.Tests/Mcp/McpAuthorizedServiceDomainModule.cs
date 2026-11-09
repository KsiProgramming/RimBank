using System.ComponentModel;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RimBank.Mcp;

namespace RimBank.Host.Tests;

/// <summary>
/// A test-only module whose tools carry the policy of their operation, as a context's <c>.Mcp</c> tools do
/// (ADR 0038, ADR 0039): one query on <see cref="ReadScope"/> and one command on <see cref="WriteScope"/>.
/// </summary>
internal sealed class McpAuthorizedServiceDomainModule : IServiceDomainModule
{
    public const string ReadScope = "fake-authorized-service-domain:read";

    public const string WriteScope = "fake-authorized-service-domain:write";

    public string ServiceDomain => "FakeAuthorizedServiceDomain";

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void AddOperationPolicies(OperationPolicies policies) =>
        policies
            .AddQuery(FakeAuthorizedServiceDomainTools.RetrieveOperationId, ReadScope)
            .AddCommand(FakeAuthorizedServiceDomainTools.RegisterOperationId, WriteScope);

    public void AddMcpTools(IMcpServerBuilder tools) => tools.WithTools<FakeAuthorizedServiceDomainTools>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

[McpServerToolType]
internal sealed class FakeAuthorizedServiceDomainTools
{
    public const string RetrieveName = "fake_authorized_service_domain_retrieve";

    public const string RetrieveOperationId = "FakeAuthorizedServiceDomainEntry/Retrieve";

    public const string RegisterName = "fake_authorized_service_domain_register";

    public const string RegisterOperationId = "FakeAuthorizedServiceDomainEntry/Register";

    [McpServerTool(Name = RetrieveName, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [ToolOperation(RetrieveOperationId)]
    [Description("Retrieves the fake authorized service domain's fixed entry.")]
    public static string Retrieve() => "retrieved";

    [McpServerTool(Name = RegisterName, ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [ToolOperation(RegisterOperationId)]
    [Description("Registers nothing; it only proves the command policy.")]
    public static string Register() => "registered";
}
