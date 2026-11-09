using System.Globalization;
using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol.Protocol;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

/// <summary>
/// The checks that a tool answers as its HTTP endpoint does (mission principles 6 and 8, tech-stack §6, ADR 0039): the
/// same body with the <c>ETag</c> as <c>version</c>, or the same Problem Details.
/// </summary>
public static class Parity
{
    /// <summary>The Problem Details members that do not depend on the request.</summary>
    private static readonly string[] _problemMembers = ["type", "title", "status", "detail", "errorCode", "errors"];

    /// <summary>Sends <paramref name="body"/> to <paramref name="url"/> with every scope, as a command or a query.</summary>
    /// <param name="host">The host.</param>
    /// <param name="method">GET, POST or PUT.</param>
    /// <param name="url">The BIAN route, with its ids.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="body">The JSON body of a command.</param>
    /// <param name="key">The <c>Idempotency-Key</c> of a command.</param>
    /// <param name="ifMatch">The expected version of a command that changes a record.</param>
    /// <returns>The scenario's result.</returns>
    public static Task<IScenarioResult> HttpAsync(
        PartyMcpHost host,
        HttpMethod method,
        string url,
        int status,
        string? body = null,
        string? key = null,
        long? ifMatch = null) =>
        host.Alba.Scenario(_ =>
        {
            switch (method.Method)
            {
                case "GET":
                    _.Get.Url(url);
                    break;
                case "POST":
                    _.Post.Text(body ?? "{}").ToUrl(url);
                    break;
                case "PUT":
                    _.Put.Text(body ?? "{}").ToUrl(url);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, "Only GET, POST and PUT are BIAN routes here.");
            }

            if (body is not null)
            {
                _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            }

            if (key is not null)
            {
                _.WithRequestHeader("Idempotency-Key", key);
            }

            if (ifMatch is { } version)
            {
                _.WithRequestHeader(HeaderNames.IfMatch, $"\"{version.ToString(CultureInfo.InvariantCulture)}\"");
            }

            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.StatusCodeShouldBe(status);
        });

    /// <summary>
    /// Checks that the tool's result is the HTTP body plus <c>version</c>, and that <c>version</c> is the <c>ETag</c>.
    /// Members whose value depends on the call (an id the command created, the time) are compared by presence only.
    /// </summary>
    /// <param name="http">The HTTP answer.</param>
    /// <param name="tool">The tool's answer.</param>
    /// <param name="volatileMembers">Members that differ between two calls, such as a new id.</param>
    public static async Task ShouldMirrorAsync(IScenarioResult http, CallToolResult tool, params string[] volatileMembers)
    {
        var body = await http.ReadAsJsonAsync<JsonElement>();
        var result = tool.Success();

        Members(result).ShouldBe([.. Members(body), "version"], ignoreOrder: true);
        foreach (var member in Members(body).Except(volatileMembers, StringComparer.Ordinal))
        {
            JsonElement.DeepEquals(result.GetProperty(member), body.GetProperty(member))
                .ShouldBeTrue($"{member}: {result.GetProperty(member)} vs {body.GetProperty(member)}");
        }

        ETagOf(http).ShouldBe(result.GetProperty("version").GetInt64());
    }

    /// <summary>Checks that the tool's error is the HTTP Problem Details, member by member.</summary>
    /// <param name="http">The HTTP answer.</param>
    /// <param name="tool">The tool's answer.</param>
    public static async Task ShouldBeSameProblemAsync(IScenarioResult http, CallToolResult tool)
    {
        var expected = await http.ReadAsJsonAsync<JsonElement>();
        http.Context.Response.ContentType.ShouldBe("application/problem+json");
        var actual = tool.Problem(expected.GetProperty("status").GetInt32(), expected.GetProperty("errorCode").GetString()!);
        foreach (var member in _problemMembers)
        {
            expected.TryGetProperty(member, out var wanted).ShouldBe(actual.TryGetProperty(member, out var got), member);
            if (wanted.ValueKind != JsonValueKind.Undefined)
            {
                JsonElement.DeepEquals(got, wanted).ShouldBeTrue($"{member}: {got} vs {wanted}");
            }
        }
    }

    /// <summary>The version an HTTP answer sent as its <c>ETag</c>.</summary>
    public static long ETagOf(IScenarioResult http) =>
        long.Parse(http.Context.Response.Headers.ETag.ToString().Trim('"'), CultureInfo.InvariantCulture);

    /// <summary><paramref name="element"/> without <paramref name="member"/>.</summary>
    public static JsonElement Without(JsonElement element, string member) =>
        JsonSerializer.SerializeToElement(
            element.EnumerateObject().Where(property => property.Name != member).ToDictionary(property => property.Name, property => property.Value));

    private static string[] Members(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
