using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RimBank.Host.Tests;

// Roadmap 1.16: a tool carries its operation's policy, as its endpoint does (ADR 0038). A caller without the scope does
// not see the tool, and a call is refused with the shared.forbidden Problem Details HTTP returns (ADR 0039).
public sealed class McpToolAuthorizationTests
{
    [Fact]
    public async Task The_tool_list_holds_only_the_tools_the_scopes_allow()
    {
        await using var app = await McpTestHost.StartAsync(new McpAuthorizedServiceDomainModule());
        await using var client = await ConnectAsync(app, McpAuthorizedServiceDomainModule.ReadScope);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        tools.Select(tool => tool.Name).ShouldBe([FakeAuthorizedServiceDomainTools.RetrieveName]);
    }

    [Theory]
    [InlineData(McpAuthorizedServiceDomainModule.ReadScope, FakeAuthorizedServiceDomainTools.RegisterName)]
    [InlineData(McpAuthorizedServiceDomainModule.WriteScope, FakeAuthorizedServiceDomainTools.RetrieveName)]
    public async Task A_call_without_the_operations_scope_is_a_forbidden_problem(string scope, string tool)
    {
        await using var app = await McpTestHost.StartAsync(new McpAuthorizedServiceDomainModule());
        await using var client = await ConnectAsync(app, scope);

        var result = await client.CallToolAsync(tool, cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.ShouldBe(true);
        var problem = result.StructuredContent!.Value;
        problem.GetProperty("status").GetInt32().ShouldBe(403);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.forbidden");
        problem.GetProperty("instance").GetString().ShouldBe(McpComposition.EndpointPath);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        JsonElement.Parse(((TextContentBlock)result.Content.Single()).Text).GetRawText().ShouldBe(problem.GetRawText());
    }

    [Theory]
    [InlineData(McpAuthorizedServiceDomainModule.ReadScope, FakeAuthorizedServiceDomainTools.RetrieveName, "retrieved")]
    [InlineData(McpAuthorizedServiceDomainModule.WriteScope, FakeAuthorizedServiceDomainTools.RegisterName, "registered")]
    public async Task A_call_with_the_operations_scope_runs_the_tool(string scope, string tool, string answer)
    {
        await using var app = await McpTestHost.StartAsync(new McpAuthorizedServiceDomainModule());
        await using var client = await ConnectAsync(app, scope);

        var result = await client.CallToolAsync(tool, cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.ShouldNotBe(true);
        ((TextContentBlock)result.Content.Single()).Text.ShouldBe(answer);
    }

    private static Task<McpClient> ConnectAsync(WebApplication app, string scope)
    {
        var http = TestTokens.Authenticated(app.GetTestClient(), [scope]);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpComposition.EndpointPath),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            ownsHttpClient: true);
        return McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }
}
