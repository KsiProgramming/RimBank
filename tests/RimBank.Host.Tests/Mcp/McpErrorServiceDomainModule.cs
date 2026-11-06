using System.ComponentModel;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Host.Modules;
using RimBank.Mcp.Errors;
using RimBank.SharedKernel;
using Result = RimBank.SharedKernel.Result;

namespace RimBank.Host.Mcp.Tests;

/// <summary>
/// A test-only module whose tools fail in each way a real tool can (ADR 0022): a query that finds nothing, a command
/// whose input is invalid, and a tool that throws. They return their failures through <c>RimBank.Mcp</c>, as a
/// context's <c>.Mcp</c> adapter will.
/// </summary>
internal sealed class McpErrorServiceDomainModule : IServiceDomainModule
{
    public string ServiceDomain => "FakeErrorServiceDomain";

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void AddMcpTools(IMcpServerBuilder tools) => tools.WithTools<FakeErrorServiceDomainTools>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

[McpServerToolType]
internal sealed class FakeErrorServiceDomainTools
{
    public const string RetrieveName = "fake_error_service_domain_retrieve";

    public const string RegisterName = "fake_error_service_domain_register";

    public const string UpdateName = "fake_error_service_domain_update";

    /// <summary>Text that must never reach a client: it stands for a connection string in an exception message.</summary>
    public const string SecretMessage = "Host=ledger-db;Password=hunter2";

    public static Error EntryNotFound { get; } =
        Error.NotFound("fake.entry-not-found", "No fake service domain entry has this identifier.");

    public static Error InvalidRegistration { get; } = Error.Validation(
        "fake.registration-invalid",
        "The registration is invalid.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["name"] = ["The name is required."] });

    [McpServerTool(Name = RetrieveName, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Retrieves a fake service domain entry; there is never one.")]
    public static CallToolResult Retrieve(string entryId) =>
        Result.Failure<string>(EntryNotFound).ToToolResult(view => new CallToolResult { Content = [new TextContentBlock { Text = $"{entryId}: {view}" }] });

    [McpServerTool(Name = RegisterName, ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Registers a fake service domain entry; the registration is always invalid.")]
    public static CallToolResult Register(string name, int quantity) =>
        Result.Failure<string>(InvalidRegistration).ToToolResult(id => new CallToolResult { Content = [new TextContentBlock { Text = $"{id}: {name} x {quantity}" }] });

    [McpServerTool(Name = UpdateName, ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Updates a fake service domain entry; it always fails unexpectedly.")]
    public static string Update() => throw new InvalidOperationException($"Could not reach the store at {SecretMessage}.");
}
