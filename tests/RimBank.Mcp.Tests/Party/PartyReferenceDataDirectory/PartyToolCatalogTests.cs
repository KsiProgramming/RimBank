using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using RimBank.Host;
using RimBank.Mcp;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.16 and 1.17: the tool list is exactly the implemented BIAN operations, each tool is named after its
// operation, carries its operation's policy, the annotations of its kind and the arguments of its kind (ADR 0021,
// ADR 0038, ADR 0039, ADR 0040).
public sealed class PartyToolCatalogTests(PostgresqlServer server)
{
    private const string ServiceDomainTool = "party_reference_data_directory";

    private const string ControlRecord = "PartyReferenceDataDirectoryEntry";

    /// <summary>The Control Record Register's one body field, a flat argument since 1.16 (ADR 0039, ADR 0040).</summary>
    private const string RegisterPartyFlatBody = "partyType";

    /// <summary>The arguments a command tool takes in addition to its ids (ADR 0018, ADR 0027, ADR 0040).</summary>
    private static readonly string[] _commandArguments = ["request", "expectedVersion", "idempotencyKey"];

    [Fact]
    public async Task The_tool_list_is_the_implemented_operations()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();
        var client = await host.ConnectAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        tools.Count.ShouldBe(13);
        tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ShouldBe(
            catalog.Policies.Select(policy => ToolName(policy.OperationId)).Order(StringComparer.Ordinal));
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
            var policy = PolicyOf(tool, catalog);
            tool.ProtocolTool.Name.ShouldBe(ToolName(policy.OperationId));
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
    public async Task Each_tool_requires_the_ids_and_the_arguments_of_its_kind()
    {
        await using var host = await server.StartHostAsync();
        var catalog = host.Alba.Services.GetRequiredService<OperationPolicyCatalog>();

        foreach (var tool in host.Alba.Services.GetServices<McpServerTool>())
        {
            var policy = PolicyOf(tool, catalog);
            var schema = tool.ProtocolTool.InputSchema;
            var properties = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToList();
            var required = schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()!).ToList();

            required.ShouldBe(properties, ignoreOrder: true, tool.ProtocolTool.Name);
            properties.Except([.. _commandArguments, RegisterPartyFlatBody])
                .ShouldAllBe(name => name.EndsWith("Id", StringComparison.Ordinal), tool.ProtocolTool.Name);
            properties.Intersect(_commandArguments).Order(StringComparer.Ordinal).ShouldBe(
                ArgumentsOf(policy).Order(StringComparer.Ordinal),
                tool.ProtocolTool.Name);
        }
    }

    [Fact]
    public async Task A_command_tools_request_is_the_bian_body_with_its_property_names()
    {
        await using var host = await server.StartHostAsync();
        var reference = host.Alba.Services.GetServices<McpServerTool>().Single(tool => tool.ProtocolTool.Name == UpdatePartyReferenceTool.Name);

        var request = reference.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request");

        request.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldContain("ResidentialAddress");
        request.GetProperty("properties").GetProperty("ResidentialAddress").ToString().ShouldContain("TownName");
        request.GetProperty("properties").EnumerateObject().Select(property => property.Name).ShouldContain("eMailAddress");
    }

    [Fact]
    public async Task A_caller_with_only_the_read_scope_sees_only_the_retrieve_tools_and_cannot_call_a_command()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var client = await host.ConnectAsync([PartyReferenceDataDirectoryModule.ReadScope]);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var update = await host.CallAsync(
            UpdatePartyReferenceTool.Name,
            new()
            {
                ["partyReferenceDataDirectoryEntryId"] = entry.Id.ToString(),
                ["referenceId"] = entry.ReferenceId.ToString(),
                ["request"] = JsonElement.Parse("""{ "PartyFullLegalName": "Jeanne Dupont" }"""),
                ["expectedVersion"] = entry.Version,
                ["idempotencyKey"] = PartyTools.Key("denied"),
            },
            [PartyReferenceDataDirectoryModule.ReadScope]);

        tools.Select(tool => tool.Name).ShouldAllBe(name => name.StartsWith($"{ServiceDomainTool}_retrieve", StringComparison.Ordinal));
        tools.Count.ShouldBe(5);
        update.Problem(403, "shared.forbidden");
        (await host.ScalarAsync<long>(PartyTools.EventsOf(entry.Id))).ShouldBe(1);
    }

    /// <summary>
    /// The arguments a tool takes besides its ids: none for a query; for a command, a required <c>idempotencyKey</c>
    /// (ADR 0018), an <c>expectedVersion</c> when it changes a record (ADR 0027), and a <c>request</c> body unless it is
    /// the Control Record's Register, whose only field is a flat <c>partyType</c> (ADR 0039, ADR 0040).
    /// </summary>
    private static IEnumerable<string> ArgumentsOf(OperationPolicy policy)
    {
        if (policy.Kind == OperationKind.Query)
        {
            yield break;
        }

        yield return "idempotencyKey";
        if (!policy.OperationId.EndsWith("/Register", StringComparison.Ordinal))
        {
            yield return "expectedVersion";
        }

        if (policy.OperationId != RegisterPartyTool.OperationId)
        {
            yield return "request";
        }
    }

    private static OperationPolicy PolicyOf(McpServerTool tool, OperationPolicyCatalog catalog)
    {
        var operationId = tool.Metadata.OfType<ToolOperationAttribute>().ShouldHaveSingleItem().OperationId;
        return catalog.Find(operationId).ShouldNotBeNull($"{tool.ProtocolTool.Name} names an operation no policy protects");
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
