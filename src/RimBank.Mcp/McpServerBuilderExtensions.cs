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

    /// <summary>
    /// Makes every tool's <see cref="ToolOperationAttribute"/> apply (ADR 0038, ADR 0039): a caller who does not meet the
    /// operation's policy does not see the tool in <c>tools/list</c>, and a call gets the <c>shared.forbidden</c> Problem
    /// Details as an error result (<see cref="ToolAuthorizationFilter"/>).
    /// </summary>
    /// <param name="mcp">The MCP server builder, after <see cref="AddRimBankToolErrors"/>.</param>
    /// <returns>The same builder.</returns>
    public static IMcpServerBuilder AddRimBankToolAuthorization(this IMcpServerBuilder mcp)
    {
        ArgumentNullException.ThrowIfNull(mcp);

        mcp.Services.AddHttpContextAccessor();
        return mcp.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(ToolAuthorizationFilter.WrapCall);
            filters.AddListToolsFilter(ToolAuthorizationFilter.WrapList);
        });
    }
}
