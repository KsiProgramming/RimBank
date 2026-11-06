using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using RimBank.Host.Security.Tests;

namespace RimBank.Host.Errors.Tests;

// Every non-2xx response of the host is RFC 9457 Problem Details, in every environment (tech-stack §5, ADR 0015). The
// requests are authenticated; the 401 and 403 are covered by AuthenticationTests (ADR 0019).
public sealed class ProblemDetailsEndpointTests
{
    private const string ProblemJson = "application/problem+json";

    public static TheoryData<string> Environments => ["Development", "Production"];

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task An_unknown_route_is_a_shared_not_found_problem(string environment)
    {
        using var response = await GetAsync(environment, "/PartyReferenceDataDirectory/does-not-exist/Retrieve");
        var problem = await ProblemOf(response, HttpStatusCode.NotFound);

        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:shared:not-found");
        problem.GetProperty("title").GetString().ShouldBe("Not Found");
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("detail").GetString().ShouldBe("No resource exists at this address.");
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.not-found");
        problem.GetProperty("instance").GetString().ShouldBe("/PartyReferenceDataDirectory/does-not-exist/Retrieve");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task An_exception_is_a_generic_unexpected_problem_that_reveals_nothing(string environment)
    {
        using var response = await GetAsync(environment, "/faulty/boom");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = await ProblemOf(response, HttpStatusCode.InternalServerError);

        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:shared:unexpected");
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.unexpected");
        problem.GetProperty("instance").GetString().ShouldBe("/faulty/boom");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        body.ShouldNotContain(FaultyServiceDomainModule.Secret);
        body.ShouldNotContain(nameof(InvalidOperationException));
        body.ShouldNotContain("   at ");
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task An_error_returned_by_an_endpoint_keeps_its_context_urn(string environment)
    {
        using var response = await GetAsync(environment, "/faulty/missing");
        var problem = await ProblemOf(response, HttpStatusCode.NotFound);

        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:sample:sample-not-found");
        problem.GetProperty("errorCode").GetString().ShouldBe("sample.sample-not-found");
        problem.GetProperty("detail").GetString().ShouldBe("No sample with this identifier.");
        problem.GetProperty("instance").GetString().ShouldBe("/faulty/missing");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task A_validation_error_lists_its_field_errors(string environment)
    {
        using var response = await GetAsync(environment, "/faulty/invalid");
        var problem = await ProblemOf(response, HttpStatusCode.BadRequest);

        problem.GetProperty("errorCode").GetString().ShouldBe("sample.invalid-sample");
        problem.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("The name is required.");
    }

    [Fact]
    public async Task The_trace_id_is_the_callers_W3C_trace_id()
    {
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        await using var app = await StartAsync("Production");
        using var client = TestTokens.Authenticated(app.GetTestClient());
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/faulty/missing", UriKind.Relative));
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var problem = await ProblemOf(response, HttpStatusCode.NotFound);

        problem.GetProperty("traceId").GetString().ShouldBe(traceId);
    }

    [Fact]
    public async Task The_health_probes_are_unaffected()
    {
        using var response = await GetAsync("Production", "/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
    }

    private static async Task<HttpResponseMessage> GetAsync(string environment, string path)
    {
        await using var app = await StartAsync(environment);
        using var client = TestTokens.Authenticated(app.GetTestClient());
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        await response.Content.LoadIntoBufferAsync(TestContext.Current.CancellationToken);
        return response;
    }

    private static async Task<JsonElement> ProblemOf(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<WebApplication> StartAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.UseTestTokens();
        var app = HostComposition.Build(builder, [new FaultyServiceDomainModule()]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
