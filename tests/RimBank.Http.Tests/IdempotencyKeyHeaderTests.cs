using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RimBank.Problems;
using RimBank.SharedKernel;

namespace RimBank.Http.Tests;

// Roadmap 0.12: every state-changing endpoint requires an Idempotency-Key header, and a missing, invalid, reused or
// in-use key is RFC 9457 Problem Details (specs/tech-stack.md §5, ADR 0015, ADR 0018). Queries need no key.
public sealed class IdempotencyKeyHeaderTests
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task A_command_without_the_header_is_a_400_missing_key_problem()
    {
        using var response = await SendAsync(HttpMethod.Post, "/sample/Register");

        var problem = await ProblemOf(response, HttpStatusCode.BadRequest);
        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:shared:idempotency-key-missing");
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-missing");
    }

    [Theory]
    [InlineData("two words")]
    [InlineData("clé")]
    public async Task A_command_with_an_invalid_key_is_a_400_invalid_key_problem(string key)
    {
        using var response = await SendAsync(HttpMethod.Post, "/sample/Register", key);

        var problem = await ProblemOf(response, HttpStatusCode.BadRequest);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-invalid");
    }

    [Fact]
    public async Task A_command_with_several_keys_is_a_400_invalid_key_problem()
    {
        using var response = await SendAsync(HttpMethod.Post, "/sample/Register", "first", "second");

        var problem = await ProblemOf(response, HttpStatusCode.BadRequest);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-invalid");
    }

    [Fact]
    public async Task A_valid_key_reaches_the_endpoint_as_sent()
    {
        using var response = await SendAsync(HttpMethod.Post, "/sample/Register", "0193a5b2-7c4e-7d10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("0193a5b2-7c4e-7d10");
    }

    [Theory]
    [InlineData("/sample/Reused", "shared.idempotency-key-reused")]
    [InlineData("/sample/InUse", "shared.idempotency-key-in-use")]
    public async Task An_idempotency_conflict_from_the_command_is_a_409_problem(string path, string errorCode)
    {
        using var response = await SendAsync(HttpMethod.Post, path, "k-1");

        var problem = await ProblemOf(response, HttpStatusCode.Conflict);
        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:" + errorCode.Replace('.', ':'));
        problem.GetProperty("errorCode").GetString().ShouldBe(errorCode);
    }

    [Fact]
    public async Task A_query_needs_no_key()
    {
        using var response = await SendAsync(HttpMethod.Get, "/sample/1/Retrieve");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_command_endpoint_declares_that_it_requires_the_header()
    {
        await using var app = await StartAsync();
        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/sample/Register").Metadata.GetMetadata<IdempotencyKeyRequirement>()
            .ShouldNotBeNull().HeaderName.ShouldBe("Idempotency-Key");
        endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/sample/{id}/Retrieve").Metadata.GetMetadata<IdempotencyKeyRequirement>()
            .ShouldBeNull();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, params string[] keys)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (keys.Length > 0)
        {
            request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader.HeaderName, keys);
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await response.Content.LoadIntoBufferAsync(TestContext.Current.CancellationToken);
        return response;
    }

    private static async Task<JsonElement> ProblemOf(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    // A test-only API shaped like a .Api adapter: command endpoints require the key, the query endpoint does not.
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        var commands = app.MapGroup("/sample").RequireIdempotencyKey();
        commands.MapPost("/Register", (IdempotencyKeyHeader idempotency) => idempotency.Value.Value);
        commands.MapPost("/Reused", (IdempotencyKeyHeader idempotency) => IdempotencyKey.Reused.ToHttpResult());
        commands.MapPost("/InUse", (IdempotencyKeyHeader idempotency) => IdempotencyKey.InUse.ToHttpResult());
        app.MapGet("/sample/{id}/Retrieve", (string id) => Results.Ok(id));

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
