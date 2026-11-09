using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using RimBank.Host;
using RimBank.Mcp;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.16, pulled forward from 1.17: the tool list matches the implemented BIAN operations, each tool is named after
// its operation, carries its operation's policy and the annotations of its kind (ADR 0021, ADR 0038, ADR 0039).
public sealed class PartyToolCatalogTests(PostgresqlServer server)
{
    private const string ServiceDomainTool = "party_reference_data_directory";

    private const string ControlRecord = "PartyReferenceDataDirectoryEntry";

    /// <summary>The implemented operations whose tools roadmap 1.17 adds. 1.17 empties this list.</summary>
    private static readonly string[] _toolsIn117 =
    [
        "PartyReferenceDataDirectoryEntry/Update",
        "Demographics/Update",
        "Demographics/Exchange",
        "Demographics/Retrieve",
        "Reference/Update",
        "Reference/Retrieve",
        "Associations/Register",
        "Associations/Update",
        "Associations/Retrieve",
        "BankRelations/Update",
        "BankRelations/Retrieve",
    ];

    [Fact]
    public async Task The_tool_list_is_the_implemented_operations_that_have_their_tool_yet()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();
        var client = await host.ConnectAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        _toolsIn117.ShouldBeSubsetOf(catalog.Policies.Select(policy => policy.OperationId));
        tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ShouldBe(
            catalog.Policies
                .Select(policy => policy.OperationId)
                .Except(_toolsIn117, StringComparer.Ordinal)
                .Select(ToolName)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Each_tool_carries_its_operations_policy_and_the_annotations_of_its_kind()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();
        var tools = host.Alba.Services.GetServices<McpServerTool>().ToList();

        tools.ShouldNotBeEmpty();
        foreach (var tool in tools)
        {
            var operationId = tool.Metadata.OfType<ToolOperationAttribute>().ShouldHaveSingleItem().OperationId;
            var policy = catalog.Find(operationId).ShouldNotBeNull($"{tool.ProtocolTool.Name} names an operation no policy protects");
            tool.ProtocolTool.Name.ShouldBe(ToolName(operationId));
            tool.ProtocolTool.Description.ShouldNotBeNullOrWhiteSpace();
            tool.ProtocolTool.InputSchema.GetProperty("properties").EnumerateObject().ShouldNotBeEmpty();
            var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
            annotations.ReadOnlyHint.ShouldBe(policy.Kind == OperationKind.Query, tool.ProtocolTool.Name);
            annotations.DestructiveHint.ShouldBe(false, tool.ProtocolTool.Name);
            annotations.IdempotentHint.ShouldBe(true, tool.ProtocolTool.Name);
            annotations.OpenWorldHint.ShouldBe(false, tool.ProtocolTool.Name);
        }
    }

    [Fact]
    public async Task A_command_tool_requires_its_idempotency_key()
    {
        await using var host = await server.StartHostAsync();

        var register = host.Alba.Services.GetServices<McpServerTool>().Single(tool => tool.ProtocolTool.Name == RegisterPartyTool.Name);

        register.ProtocolTool.InputSchema.GetProperty("required").EnumerateArray().Select(name => name.GetString())
            .ShouldBe(["partyType", "idempotencyKey"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_query_tool_takes_no_idempotency_key()
    {
        await using var host = await server.StartHostAsync();

        var retrieve = host.Alba.Services.GetServices<McpServerTool>()
            .Single(tool => tool.ProtocolTool.Name == RetrievePartyReferenceDataDirectoryEntryTool.Name);

        retrieve.ProtocolTool.InputSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["partyReferenceDataDirectoryEntryId"]);
        retrieve.ProtocolTool.InputSchema.GetProperty("required").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    /// <summary>
    /// The tool name of a BIAN operation (ADR 0021): <c>&lt;service_domain&gt;_&lt;operation&gt;</c> for the Control
    /// Record, <c>&lt;service_domain&gt;_&lt;operation&gt;_&lt;behavior_qualifier&gt;</c> for a Behavior Qualifier.
    /// </summary>
    private static string ToolName(string operationId)
    {
        var (target, operation) = operationId.Split('/') switch
        {
            [var first, var second] => (first, second),
            _ => throw new ArgumentException($"'{operationId}' is not a BIAN operation id.", nameof(operationId)),
        };
        return target == ControlRecord
            ? $"{ServiceDomainTool}_{SnakeCase(operation)}"
            : $"{ServiceDomainTool}_{SnakeCase(operation)}_{SnakeCase(target)}";
    }

    private static string SnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
}
