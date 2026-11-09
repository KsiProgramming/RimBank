using Microsoft.AspNetCore.Authorization;

namespace RimBank.Host;

/// <summary>The caller's access token must carry <paramref name="Scope"/> (ADR 0038).</summary>
/// <param name="Scope">The required OAuth scope, for example <c>party-reference-data-directory:write</c>.</param>
public sealed record OperationScopeRequirement(string Scope) : IAuthorizationRequirement;

/// <summary>
/// Meets an <see cref="OperationScopeRequirement"/> when the token's <c>scope</c> claim, a space-separated list
/// (RFC 9068, as Keycloak issues it), holds the required scope exactly. A prefix or a longer scope does not count.
/// </summary>
/// <remarks>
/// A failed check is a bodiless 403, which the status-code pages turn into <c>shared.forbidden</c> (ADR 0015). The name
/// does not end in <c>Handler</c>: Wolverine would take a <c>…Handler</c> type of the host's assembly for a message handler.
/// </remarks>
public sealed class OperationScopeAuthorization : AuthorizationHandler<OperationScopeRequirement>
{
    /// <summary>The claim that holds the token's granted scopes.</summary>
    public const string ScopeClaim = "scope";

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OperationScopeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var granted = context.User.FindAll(ScopeClaim)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (granted.Contains(requirement.Scope, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
