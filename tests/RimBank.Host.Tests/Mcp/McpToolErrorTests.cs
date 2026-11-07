using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace RimBank.Host.Tests;

// Roadmap 0.16: a failing tool answers with isError and the Problem Details HTTP would return for the same error, and
// an unexpected failure leaks nothing (specs/tech-stack.md §6, ADR 0021, ADR 0022).
public sealed class McpToolErrorTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    [Fact]
    public async Task A_failed_result_is_an_isError_problem_details_result()
    {
        var result = await CallAsync(FakeErrorServiceDomainTools.RetrieveName, new() { ["entryId"] = "42" });

        result.IsError.ShouldBe(true);
        var problem = StructuredProblem(result);
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:fake:entry-not-found");
        problem.GetProperty("errorCode").GetString().ShouldBe(FakeErrorServiceDomainTools.EntryNotFound.Code);
        problem.GetProperty("detail").GetString().ShouldBe(FakeErrorServiceDomainTools.EntryNotFound.Message);
        problem.GetProperty("instance").GetString().ShouldBe(McpComposition.EndpointPath);
        TextProblem(result).GetRawText().ShouldBe(problem.GetRawText());
    }

    [Fact]
    public async Task A_validation_failure_carries_its_field_errors()
    {
        var result = await CallAsync(FakeErrorServiceDomainTools.RegisterName, new() { ["name"] = "", ["quantity"] = 1 });

        result.IsError.ShouldBe(true);
        var problem = StructuredProblem(result);
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errorCode").GetString().ShouldBe(FakeErrorServiceDomainTools.InvalidRegistration.Code);
        problem.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("The name is required.");
    }

    [Fact]
    public async Task A_throwing_tool_is_shared_unexpected_without_exception_text()
    {
        var result = await CallAsync(FakeErrorServiceDomainTools.UpdateName, []);

        result.IsError.ShouldBe(true);
        var problem = StructuredProblem(result);
        problem.GetProperty("status").GetInt32().ShouldBe(500);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.unexpected");
        var everything = JsonSerializer.Serialize(result);
        everything.ShouldNotContain(FakeErrorServiceDomainTools.SecretMessage);
        everything.ShouldNotContain(nameof(InvalidOperationException));
    }

    [Theory]
    [InlineData(FakeErrorServiceDomainTools.RetrieveName)]
    [InlineData(FakeErrorServiceDomainTools.UpdateName)]
    public async Task The_traceId_of_an_error_result_is_the_request_trace(string tool)
    {
        var result = await CallAsync(tool, new() { ["entryId"] = "42" }, traceParent: $"00-{TraceId}-00f067aa0ba902b7-01");

        StructuredProblem(result).GetProperty("traceId").GetString().ShouldBe(TraceId);
    }

    [Fact]
    public async Task An_unknown_tool_stays_a_json_rpc_error()
    {
        var exception = await Should.ThrowAsync<McpProtocolException>(() => CallAsync("fake_error_service_domain_unknown", []));

        exception.ErrorCode.ShouldBe(McpErrorCode.InvalidParams);
    }

    [Fact]
    public async Task Malformed_arguments_are_a_validation_problem_that_does_not_echo_them()
    {
        var result = await CallAsync(FakeErrorServiceDomainTools.RegisterName, new() { ["name"] = "n", ["quantity"] = "many-secret" });

        result.IsError.ShouldBe(true);
        var problem = StructuredProblem(result);
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.tool-arguments-invalid");
        JsonSerializer.Serialize(result).ShouldNotContain("many-secret");
    }

    [Fact]
    public async Task A_missing_argument_is_a_validation_problem()
    {
        var result = await CallAsync(FakeErrorServiceDomainTools.RegisterName, new() { ["name"] = "n" });

        result.IsError.ShouldBe(true);
        var problem = StructuredProblem(result);
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errorCode").GetString().ShouldBe("shared.tool-arguments-invalid");
    }

    private static async Task<CallToolResult> CallAsync(string tool, Dictionary<string, object?> arguments, string? traceParent = null)
    {
        await using var app = await McpTestHost.StartAsync(new McpErrorServiceDomainModule());
        await using var client = await McpTestHost.ConnectAsync(app, traceParent);
        return await client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static JsonElement StructuredProblem(CallToolResult result) =>
        result.StructuredContent.ShouldNotBeNull();

    private static JsonElement TextProblem(CallToolResult result) =>
        JsonElement.Parse(result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text);
}
