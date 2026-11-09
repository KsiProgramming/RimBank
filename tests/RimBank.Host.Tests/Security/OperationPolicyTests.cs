using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RimBank.Host.Tests;

// Roadmap 1.15: each BIAN operation has its own policy, named by the operation id, which requires the context's write
// scope (a command) or read scope (a query); a token without it is a 403 shared.forbidden problem (ADR 0038).
public sealed class OperationPolicyTests
{
    private const string ProblemJson = "application/problem+json";

    [Theory]
    [InlineData(ScopedServiceDomainModule.WriteScope)]
    [InlineData("openid " + ScopedServiceDomainModule.WriteScope + " profile")]
    public async Task A_token_with_the_operations_scope_reaches_the_endpoint(string scope)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(Post(ScopedServiceDomainModule.CommandRoute, TestTokens.Issue(scopes: [scope])), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("no scope", new string[0])]
    [InlineData("the other kind's scope", new[] { ScopedServiceDomainModule.ReadScope })]
    [InlineData("a prefix of the scope", new[] { "sample-ledger:wri" })]
    [InlineData("a longer scope", new[] { ScopedServiceDomainModule.WriteScope + "r" })]
    [InlineData("another case", new[] { "SAMPLE-LEDGER:WRITE" })]
    public async Task A_token_without_the_operations_scope_is_a_shared_forbidden_problem(string reason, string[] scopes)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(Post(ScopedServiceDomainModule.CommandRoute, TestTokens.Issue(scopes: scopes)), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, reason);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("shared.forbidden");
        problem.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_query_needs_the_read_scope_and_a_command_the_write_scope()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var reader = TestTokens.Issue(scopes: [ScopedServiceDomainModule.ReadScope]);
        var writer = TestTokens.Issue(scopes: [ScopedServiceDomainModule.WriteScope]);

        using var readerQuery = await client.SendAsync(TestTokens.Get(ScopedServiceDomainModule.QueryRoute, reader), TestContext.Current.CancellationToken);
        using var writerQuery = await client.SendAsync(TestTokens.Get(ScopedServiceDomainModule.QueryRoute, writer), TestContext.Current.CancellationToken);
        using var readerCommand = await client.SendAsync(Post(ScopedServiceDomainModule.CommandRoute, reader), TestContext.Current.CancellationToken);

        readerQuery.StatusCode.ShouldBe(HttpStatusCode.OK);
        writerQuery.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        readerCommand.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_caller_without_a_token_is_unauthenticated_before_it_is_forbidden()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(Post(ScopedServiceDomainModule.CommandRoute, token: null), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_catalog_lists_every_registered_operation_with_its_kind_and_scope()
    {
        await using var app = await StartAsync();

        var catalog = app.Services.GetRequiredService<OperationPolicyCatalog>();

        catalog.Policies.ShouldBe(
        [
            new OperationPolicy(ScopedServiceDomainModule.CommandOperation, OperationKind.Command, ScopedServiceDomainModule.WriteScope),
            new OperationPolicy(ScopedServiceDomainModule.QueryOperation, OperationKind.Query, ScopedServiceDomainModule.ReadScope),
        ]);
        catalog.Scopes.ShouldBe([ScopedServiceDomainModule.WriteScope, ScopedServiceDomainModule.ReadScope]);
    }

    [Fact]
    public void An_operation_given_two_policies_stops_the_host_from_starting()
    {
        var exception = Should.Throw<InvalidOperationException>(() => HostComposition.Build(
            NewBuilder(),
            [new ScopedServiceDomainModule(), new ScopedServiceDomainModule("SampleLedgerAgain")]));

        exception.Message.ShouldContain(ScopedServiceDomainModule.CommandOperation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two scopes")]
    public void A_scope_must_be_one_token(string scope)
    {
        var module = new ScopedServiceDomainModule(writeScope: scope);

        Should.Throw<ArgumentException>(() => OperationPolicyCatalog.Of([module]));
    }

    private static HttpRequestMessage Post(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static WebApplicationBuilder NewBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        return builder;
    }

    private static async Task<WebApplication> StartAsync()
    {
        var app = HostComposition.Build(NewBuilder(), [new ScopedServiceDomainModule()]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}

/// <summary>
/// A test-only module with one command and one query, each protected by its operation's policy, as a real context's
/// module and <c>.Api</c> do (ADR 0038).
/// </summary>
internal sealed class ScopedServiceDomainModule(string serviceDomain = "SampleLedger", string writeScope = ScopedServiceDomainModule.WriteScope)
    : IServiceDomainModule
{
    public const string ReadScope = "sample-ledger:read";

    public const string WriteScope = "sample-ledger:write";

    public const string CommandOperation = "SampleLedgerEntry/Initiate";

    public const string QueryOperation = "SampleLedgerEntry/Retrieve";

    public const string CommandRoute = "/sample-ledger/initiate";

    public const string QueryRoute = "/sample-ledger/retrieve";

    public string ServiceDomain => serviceDomain;

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void AddOperationPolicies(OperationPolicies policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        policies
            .AddCommand(CommandOperation, writeScope)
            .AddQuery(QueryOperation, ReadScope);
    }

    public void AddMcpTools(IMcpServerBuilder tools)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(CommandRoute, () => Results.Ok()).RequireAuthorization(CommandOperation);
        endpoints.MapGet(QueryRoute, () => Results.Ok()).RequireAuthorization(QueryOperation);
    }
}
