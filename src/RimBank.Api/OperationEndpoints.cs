using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace RimBank.Api;

/// <summary>Names an endpoint after its BIAN operation and protects it with that operation's policy (ADR 0038).</summary>
public static class OperationEndpoints
{
    /// <summary>
    /// Names the endpoint <paramref name="operationId"/> and requires the authorisation policy of the same name, which
    /// the context's host module registers with the context's write scope (a command) or read scope (a query). A
    /// caller without that scope gets a 403 <c>shared.forbidden</c> Problem Details response, before the endpoint's
    /// other checks run. Use it on every BIAN endpoint, in place of <c>WithName</c>.
    /// </summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="operationId">The BIAN operation id, for example <c>PartyReferenceDataDirectoryEntry/Register</c>.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder RequireOperation(this RouteHandlerBuilder builder, string operationId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        return builder
            .WithName(operationId)
            .RequireAuthorization(operationId)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
