using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using RimBank.Problems;
using RimBank.SharedKernel;
using Result = RimBank.SharedKernel.Result;

namespace RimBank.Mcp.Errors.Tests;

// ADR 0021 and ADR 0022: an MCP error is the Problem Details HTTP returns for the same Error, as structuredContent and
// as the same JSON in one text block.
public sealed class ToolProblemTests
{
    private static readonly Error _notFound = Error.NotFound("party.party-not-found", "No party has this identifier.");

    private static readonly Error _invalid = Error.Validation(
        "party.registration-invalid",
        "The registration is invalid.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["name"] = ["The name is required.", "The name is too short."] });

    [Fact]
    public void A_not_found_error_is_an_error_result_with_problem_details()
    {
        var result = ToolProblem.Create(_notFound, McpRequest());

        result.IsError.ShouldBe(true);
        var problem = result.StructuredContent.ShouldNotBeNull();
        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:party:party-not-found");
        problem.GetProperty("title").GetString().ShouldBe("Not Found");
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("detail").GetString().ShouldBe(_notFound.Message);
        problem.GetProperty("errorCode").GetString().ShouldBe(_notFound.Code);
        problem.GetProperty("instance").GetString().ShouldBe("/mcp");
        problem.GetProperty("traceId").GetString().ShouldBe("request-trace");
    }

    [Fact]
    public void A_validation_error_carries_its_field_errors()
    {
        var problem = ToolProblem.Create(_invalid).StructuredContent.ShouldNotBeNull();

        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errors").GetProperty("name").EnumerateArray().Select(message => message.GetString())
            .ShouldBe(["The name is required.", "The name is too short."]);
    }

    [Fact]
    public void The_text_content_is_the_same_json_as_the_structured_content()
    {
        var result = ToolProblem.Create(_invalid, McpRequest());

        var text = result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text;
        text.ShouldBe(result.StructuredContent.ShouldNotBeNull().GetRawText());
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public void The_structured_content_equals_the_http_problem_details_for_the_same_error(string code)
    {
        var error = AllErrors().Single(candidate => candidate.Code == code);
        var http = ErrorProblemDetails.Create(error);
        ProblemDetailsEnrichment.Apply(http, McpRequest());

        var tool = ToolProblem.Create(error, McpRequest()).StructuredContent.ShouldNotBeNull();

        tool.GetRawText().ShouldBe(JsonSerializer.Serialize(http, http.GetType(), JsonSerializerOptions.Web));
    }

    [Fact]
    public void Enriching_a_tool_problem_adds_the_request_instance_and_trace()
    {
        var result = ToolProblem.WithRequest(ToolProblem.Create(_notFound), McpRequest());

        result.StructuredContent.ShouldNotBeNull().GetRawText()
            .ShouldBe(ToolProblem.Create(_notFound, McpRequest()).StructuredContent.ShouldNotBeNull().GetRawText());
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldContain("request-trace");
    }

    [Fact]
    public void Only_a_problem_result_can_be_enriched()
    {
        var plain = new CallToolResult { Content = [new TextContentBlock { Text = "ok" }] };

        ToolProblem.IsProblem(plain).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => ToolProblem.WithRequest(plain, McpRequest()));
    }

    [Fact]
    public void ToToolResult_maps_success_through_the_projection_and_failure_through_ToolProblem()
    {
        var success = Result.Success("party-1").ToToolResult(id => new CallToolResult { Content = [new TextContentBlock { Text = id }] });
        var failure = Result.Failure<string>(_notFound).ToToolResult(id => new CallToolResult { Content = [new TextContentBlock { Text = id }] });

        success.IsError.ShouldNotBe(true);
        success.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe("party-1");
        failure.StructuredContent.ShouldNotBeNull().GetRawText().ShouldBe(ToolProblem.Create(_notFound).StructuredContent.ShouldNotBeNull().GetRawText());
    }

    [Fact]
    public void ToToolResult_without_a_value_maps_success_and_failure()
    {
        Result.Success().ToToolResult(() => new CallToolResult()).IsError.ShouldNotBe(true);
        Result.Failure(_notFound).ToToolResult(() => new CallToolResult()).IsError.ShouldBe(true);
    }

    public static TheoryData<string> Errors => [.. AllErrors().Select(error => error.Code)];

    private static IEnumerable<Error> AllErrors() =>
    [
        _notFound,
        _invalid,
        Error.Conflict("party.version-conflict", "The entry changed."),
        Error.BusinessRule("party.party-closed", "The party is closed."),
        SharedProblems.Unexpected,
        ToolErrors.ArgumentsInvalid,
    ];

    private static DefaultHttpContext McpRequest() =>
        new() { Request = { Path = "/mcp" }, TraceIdentifier = "request-trace" };
}
