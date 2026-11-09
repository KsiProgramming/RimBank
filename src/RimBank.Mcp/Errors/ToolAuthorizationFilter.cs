using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Problems;

namespace RimBank.Mcp;

/// <summary>
/// Applies each tool's <see cref="ToolOperationAttribute"/> (ADR 0038, ADR 0039): a caller who does not meet the
/// operation's policy does not see the tool in <c>tools/list</c>, and a call is refused with the
/// <c>shared.forbidden</c> (403) Problem Details HTTP returns for the same refusal, before the tool runs.
/// </summary>
/// <remarks>
/// A tool without the attribute only needs the authenticated caller that <c>/mcp</c> requires (ADR 0019). Every
/// context tool declares its operation, which the tool catalog tests check. An unknown policy name makes ASP.NET Core
/// throw, so the call fails as <c>shared.unexpected</c> and never runs without its policy.
/// </remarks>
public static class ToolAuthorizationFilter
{
    /// <summary>Wraps the next call-tool handler.</summary>
    /// <param name="next">The next handler (the tool).</param>
    /// <returns>The wrapping handler.</returns>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> WrapCall(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return async (context, cancellationToken) =>
        {
            var operationId = OperationOf(context.Server, context.Params?.Name);
            if (operationId is null || context.Services is not { } services
                || await IsAllowedAsync(services, context.User, operationId))
            {
                return await next(context, cancellationToken);
            }

            var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
            return httpContext is null
                ? ToolProblem.Create(SharedProblems.Forbidden)
                : ToolProblem.Create(SharedProblems.Forbidden, httpContext);
        };
    }

    /// <summary>Wraps the next list-tools handler.</summary>
    /// <param name="next">The next handler (the SDK's tool list).</param>
    /// <returns>The wrapping handler, which removes the tools the caller may not use.</returns>
    public static McpRequestHandler<ListToolsRequestParams, ListToolsResult> WrapList(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);
            if (context.Services is not { } services)
            {
                return result;
            }

            var allowed = new List<Tool>(result.Tools.Count);
            foreach (var tool in result.Tools)
            {
                var operationId = OperationOf(context.Server, tool.Name);
                if (operationId is null || await IsAllowedAsync(services, context.User, operationId))
                {
                    allowed.Add(tool);
                }
            }

            result.Tools = allowed;
            return result;
        };
    }

    private static string? OperationOf(McpServer server, string? toolName) =>
        toolName is not null && server.ServerOptions.ToolCollection?.TryGetPrimitive(toolName, out var tool) == true
            ? tool.Metadata.OfType<ToolOperationAttribute>().SingleOrDefault()?.OperationId
            : null;

    private static async Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal? user, string operationId) =>
        (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user ?? new ClaimsPrincipal(), operationId))
            .Succeeded;
}
