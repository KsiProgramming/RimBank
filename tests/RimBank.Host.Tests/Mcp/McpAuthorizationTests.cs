using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Client;

namespace RimBank.Host.Tests;

// Roadmap 0.16: an MCP client learns from the 401 where to get a token (MCP authorization, RFC 9728), and a call
// without a valid token is refused with the same Problem Details as HTTP (specs/tech-stack.md §6, ADR 0019, ADR 0022).
public sealed class McpAuthorizationTests
{
    private const string MetadataPath = "/.well-known/oauth-protected-resource/mcp";

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    public async Task Mcp_without_a_valid_token_is_401_problem_details_with_resource_metadata(string? token)
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        using var http = app.GetTestClient();
        using var request = ToolsList(token);

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var challenge = response.Headers.WwwAuthenticate.ShouldHaveSingleItem();
        challenge.Scheme.ShouldBe("Bearer");
        challenge.Parameter.ShouldBe($"resource_metadata=\"http://localhost{MetadataPath}\"");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
        problem.RootElement.GetProperty("instance").GetString().ShouldBe(McpComposition.EndpointPath);
        problem.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Mcp_with_a_token_from_an_untrusted_key_is_refused_the_same_way()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        using var http = app.GetTestClient();
        using var request = ToolsList(TestTokens.Issue(trusted: false));

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldHaveSingleItem().Parameter.ShouldNotBeNull().ShouldContain("resource_metadata=");
    }

    [Fact]
    public async Task An_mcp_client_without_a_token_cannot_connect()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        var http = app.GetTestClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpComposition.EndpointPath),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            ownsHttpClient: true);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken));

        exception.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_protected_resource_metadata_is_readable_without_a_token()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        using var http = app.GetTestClient();

        using var response = await http.GetAsync(new Uri(MetadataPath, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        metadata.RootElement.GetProperty("resource").GetString().ShouldBe("http://localhost/mcp");
        metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(server => server.GetString())
            .ShouldBe([TestTokens.Issuer]);
        metadata.RootElement.GetProperty("bearer_methods_supported").EnumerateArray().Select(method => method.GetString())
            .ShouldBe(["header"]);
        metadata.RootElement.GetProperty("resource_name").GetString().ShouldBe(McpComposition.ServerName);
    }

    [Fact]
    public async Task The_authorization_server_can_be_set_apart_from_the_authority()
    {
        // In Development the AppHost passes the Keycloak address an MCP client can reach, while the host itself finds
        // Keycloak through service discovery.
        const string reachable = "http://localhost:8080/realms/rimbank";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        builder.Configuration[AuthenticationComposition.AuthorizationServerKey] = reachable;
        await using var app = HostComposition.Build(builder, [new McpServiceDomainModule()]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var http = app.GetTestClient();

        using var metadata = JsonDocument.Parse(await http.GetStringAsync(new Uri(MetadataPath, UriKind.Relative), TestContext.Current.CancellationToken));

        metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(server => server.GetString())
            .ShouldBe([reachable]);
    }

    [Fact]
    public void Outside_development_the_authorization_server_must_use_https()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        builder.Configuration[AuthenticationComposition.AuthorizationServerKey] = "http://login.example.com/realms/rimbank";

        var exception = Should.Throw<InvalidOperationException>(() => HostComposition.Build(builder, [new McpServiceDomainModule()]));

        exception.Message.ShouldContain(AuthenticationComposition.AuthorizationServerKey);
    }

    [Fact]
    public async Task Http_endpoints_keep_the_plain_bearer_challenge()
    {
        await using var app = await McpTestHost.StartAsync(new McpServiceDomainModule());
        using var http = app.GetTestClient();

        using var response = await http.GetAsync(new Uri("/PartyReferenceDataDirectory/1/Retrieve", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.ShouldHaveSingleItem();
        challenge.Scheme.ShouldBe("Bearer");
        (challenge.Parameter ?? string.Empty).ShouldNotContain("resource_metadata");
    }

    private static HttpRequestMessage ToolsList(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(McpComposition.EndpointPath, UriKind.Relative))
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
