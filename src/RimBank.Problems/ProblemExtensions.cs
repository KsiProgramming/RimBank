namespace RimBank.Problems;

/// <summary>The names of RimBank's Problem Details extension members (specs/tech-stack.md §5, ADR 0015).</summary>
public static class ProblemExtensions
{
    /// <summary>The stable error code, <c>&lt;context&gt;.&lt;error-name&gt;</c>.</summary>
    public const string ErrorCode = "errorCode";

    /// <summary>The W3C trace ID of the request, to find its logs and trace.</summary>
    public const string TraceId = "traceId";

    /// <summary>Field name → messages, on validation failures only.</summary>
    public const string Errors = "errors";
}
