using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Protocol;

namespace RimBank.Host.Tests;

// Roadmap 0.15: one Streamable HTTP endpoint at /mcp, served by the host with each module's tools, protected like every
// other endpoint, and reachable by the SDK's own client (specs/tech-stack.md §6, §9, ADR 0021).
public sealed class McpEndpointTests
{
    [Fact]
    public async Task A_client_connects_and_reads_the_server_info()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        await using var client = await McpTestHost.ConnectAsync(app);

        client.ServerInfo.ShouldNotBeNull();
        client.ServerInfo.Name.ShouldBe(McpComposition.ServerName);
        client.ServerInfo.Version.ShouldBe(McpComposition.ServerVersion);
        client.ServerInfo.Version.ShouldNotContain('+');
    }

    [Fact]
    public async Task The_tool_list_contains_the_module_tool_with_its_annotations()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        await using var client = await McpTestHost.ConnectAsync(app);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var tool = tools.ShouldHaveSingleItem().ProtocolTool;
        tool.Name.ShouldBe(FakeServiceDomainTools.RetrieveName);
        tool.Description.ShouldBe(FakeServiceDomainTools.RetrieveDescription);
        tool.Annotations.ShouldNotBeNull();
        tool.Annotations.ReadOnlyHint.ShouldBe(true);
        tool.Annotations.IdempotentHint.ShouldBe(true);
        tool.Annotations.DestructiveHint.ShouldBe(false);
    }

    [Fact]
    public async Task Calling_the_tool_returns_its_result()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        await using var client = await McpTestHost.ConnectAsync(app);

        var result = await client.CallToolAsync(FakeServiceDomainTools.RetrieveName, cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.ShouldNotBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe(FakeServiceDomainTools.RetrieveResult);
    }

    [Fact]
    public async Task A_host_whose_modules_register_no_tools_offers_no_tools()
    {
        // The SDK announces the tools capability only when a tool is registered, as the MCP specification expects.
        await using var app = await McpTestHost.StartAsync(new FaultyServiceDomainModule(), new SecuredServiceDomainModule());
        await using var client = await McpTestHost.ConnectAsync(app);

        client.ServerCapabilities.Tools.ShouldBeNull();
    }

    [Fact]
    public void A_tool_name_registered_twice_stops_the_host_from_starting()
    {
        // The SDK would keep only one of the two tools, so one context could hide another's tool (ADR 0021).
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();

        var exception = Should.Throw<InvalidOperationException>(() =>
            HostComposition.Build(builder, [new McpServiceDomainModule(), new McpServiceDomainModule("AnotherFakeServiceDomain")]));

        exception.Message.ShouldContain(FakeServiceDomainTools.RetrieveName);
    }

    [Fact]
    public async Task Mcp_without_a_token_is_a_shared_unauthenticated_problem()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        using var http = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(McpComposition.EndpointPath, UriKind.Relative))
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Bearer");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
        problem.RootElement.GetProperty("instance").GetString().ShouldBe(McpComposition.EndpointPath);
        problem.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
