using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RimBank.Problems;

namespace RimBank.Http;

/// <summary>Requires the <c>Idempotency-Key</c> header on command endpoints (specs/tech-stack.md §5, ADR 0018).</summary>
public static class IdempotencyKeyEndpoints
{
    /// <summary>
    /// Answers 400 Problem Details before the endpoint runs when the request has no valid <c>Idempotency-Key</c>
    /// header: <c>shared.idempotency-key-missing</c> or <c>shared.idempotency-key-invalid</c>. Put it on every
    /// endpoint that dispatches a command, never on a <c>Retrieve</c> (query) endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">An endpoint or a route group.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder RequireIdempotencyKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(IdempotencyKeyRequirement.Instance);
        builder.AddEndpointFilter(async (context, next) =>
        {
            var key = IdempotencyKeyHeader.Read(context.HttpContext.Request);
            return key.IsSuccess ? await next(context) : key.Error.ToHttpResult();
        });
        return builder;
    }
}
