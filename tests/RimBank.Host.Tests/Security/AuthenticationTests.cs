using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace RimBank.Host.Tests;

// Roadmap 0.13: every endpoint needs a valid access token unless it is a health probe (deny by default), and a missing
// or invalid token is a 401 Problem Details response (tech-stack §5, §10, ADR 0019).
public sealed class AuthenticationTests
{
    private const string ProblemJson = "application/problem+json";

    public static TheoryData<string, string?> InvalidTokens => new()
    {
        { "none", null },
        { "malformed", "not-a-jwt" },
        { "expired", TestTokens.Issue(expires: DateTime.UtcNow.AddMinutes(-10)) },
        { "another audience", TestTokens.Issue(audience: "another-api") },
        { "signed with an unknown key", TestTokens.Issue(trusted: false) },
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task A_request_without_a_valid_token_is_a_shared_unauthenticated_problem(string reason, string? token)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/ping", token), TestContext.Current.CancellationToken);
        var problem = await ProblemOf(response, HttpStatusCode.Unauthorized);

        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:shared:unauthenticated", reason);
        problem.GetProperty("status").GetInt32().ShouldBe(401);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
        problem.GetProperty("instance").GetString().ShouldBe("/secured/ping");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        response.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task A_request_with_a_valid_token_reaches_the_endpoint()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/ping", TestTokens.Issue()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("pong");
    }

    [Fact]
    public async Task An_unknown_route_is_unauthenticated_before_it_is_not_found()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/does-not-exist", null), TestContext.Current.CancellationToken);

        (await ProblemOf(response, HttpStatusCode.Unauthorized)).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }

    [Fact]
    public async Task An_authenticated_caller_without_the_permission_is_a_shared_forbidden_problem()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/never", TestTokens.Issue()), TestContext.Current.CancellationToken);
        var problem = await ProblemOf(response, HttpStatusCode.Forbidden);

        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:shared:forbidden");
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.forbidden");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_token_without_a_subject_is_a_shared_actor_invalid_problem()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/ping", TestTokens.Issue(subject: null)), TestContext.Current.CancellationToken);
        var problem = await ProblemOf(response, HttpStatusCode.Unauthorized);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.actor-invalid");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("/health", null)]
    [InlineData("/alive", null)]
    [InlineData("/health", "not-a-jwt")]
    public async Task The_health_probes_stay_open_to_anonymous_callers(string path, string? token)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get(path, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task The_callers_subject_reaches_the_message_handler()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/who", TestTokens.Issue()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(TestTokens.Subject);
    }

    [Fact]
    public async Task The_response_never_echoes_the_token()
    {
        var token = TestTokens.Issue(audience: "another-api");
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(TestTokens.Get("/secured/ping", token), TestContext.Current.CancellationToken);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain(token);
    }

    private static async Task<JsonElement> ProblemOf(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        var app = HostComposition.Build(builder, [new SecuredServiceDomainModule()]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
