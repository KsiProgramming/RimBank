using Microsoft.Extensions.DependencyInjection;

namespace RimBank.Mcp;

/// <summary>The MCP conventions a server that hosts RimBank tools applies (ADR 0022).</summary>
public static class McpServerBuilderExtensions
{
    /// <summary>
    /// Adds the call-tool filter that gives every tool error the Problem Details shape: a tool's problem result gets the
    /// request's <c>instance</c> and <c>traceId</c>, and an unexpected failure becomes <c>shared.unexpected</c> with no
    /// internal details (<see cref="ToolErrorFilter"/>).
    /// </summary>
    /// <param name="mcp">The MCP server builder.</param>
    /// <returns>The same builder.</returns>
    public static IMcpServerBuilder AddRimBankToolErrors(this IMcpServerBuilder mcp)
    {
        ArgumentNullException.ThrowIfNull(mcp);

        mcp.Services.AddHttpContextAccessor();
        return mcp.WithRequestFilters(filters => filters.AddCallToolFilter(ToolErrorFilter.Wrap));
    }
}
