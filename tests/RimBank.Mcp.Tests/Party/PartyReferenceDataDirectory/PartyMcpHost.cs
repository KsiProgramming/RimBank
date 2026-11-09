using System.Text.Json;
using Alba;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Npgsql;
using RimBank.Host;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

/// <summary>
/// A started <c>RimBank.Host</c> on its own database, reached through the SDK's MCP client at <c>/mcp</c> and, for
/// parity tests, through HTTP (Alba). Disposing it stops the host.
/// </summary>
public sealed class PartyMcpHost(IAlbaHost host, NpgsqlDataSource administrator) : IAsyncDisposable
{
    /// <summary>The number of entry streams the context's store holds.</summary>
    public const string Streams = "SELECT count(*) FROM party_reference_data_directory.mt_streams";

    /// <summary>The number of command outcomes the idempotency policy stored (ADR 0018).</summary>
    public const string Outcomes = "SELECT count(*) FROM party_reference_data_directory.mt_doc_idempotentoutcome";

    private readonly List<McpClient> _clients = [];

    /// <summary>The host, to run HTTP scenarios against.</summary>
    public IAlbaHost Alba => host;

    /// <summary>
    /// An MCP client whose token grants <paramref name="scopes"/>, or every scope the host knows when they are not given.
    /// </summary>
    public async Task<McpClient> ConnectAsync(IEnumerable<string>? scopes = null)
    {
        var http = TestTokens.Authenticated(host.Server.CreateClient(), scopes);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpComposition.EndpointPath),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            ownsHttpClient: true);
        var client = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
        _clients.Add(client);
        return client;
    }

    /// <summary>Calls <paramref name="tool"/> with <paramref name="arguments"/>, as a client with every scope.</summary>
    public async Task<CallToolResult> CallAsync(string tool, Dictionary<string, object?> arguments, IEnumerable<string>? scopes = null)
    {
        var client = await ConnectAsync(scopes);
        return await client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>Runs a single-value query as the administrator, to look at what was stored.</summary>
    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = administrator.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }

        await host.DisposeAsync();
        await administrator.DisposeAsync();
    }
}

/// <summary>Reads what a tool answered.</summary>
public static class ToolAnswers
{
    /// <summary>The structured content of a successful result, after checking that the text block holds the same JSON.</summary>
    public static JsonElement Success(this CallToolResult result)
    {
        result.IsError.ShouldNotBe(true, Text(result));
        return SameAsText(result);
    }

    /// <summary>The Problem Details of an error result, after checking that the text block holds the same JSON.</summary>
    public static JsonElement Problem(this CallToolResult result, int status, string errorCode)
    {
        result.IsError.ShouldBe(true);
        var problem = SameAsText(result);
        problem.GetProperty("status").GetInt32().ShouldBe(status);
        problem.GetProperty("errorCode").GetString().ShouldBe(errorCode);
        problem.GetProperty("instance").GetString().ShouldBe(McpComposition.EndpointPath);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        return problem;
    }

    private static JsonElement SameAsText(CallToolResult result)
    {
        var structured = result.StructuredContent.ShouldNotBeNull();
        // The SDK writes structuredContent again on the wire (escaping '+' as +), so the values are compared.
        JsonElement.DeepEquals(JsonElement.Parse(Text(result)), structured).ShouldBeTrue(Text(result));
        return structured;
    }

    private static string Text(CallToolResult result) => ((TextContentBlock)result.Content.Single()).Text;
}
