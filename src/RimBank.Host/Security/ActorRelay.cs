using System.Security.Claims;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Host.Security;

/// <summary>
/// Hands the authenticated caller to the message bus, so every command and query dispatched for this request carries
/// its <see cref="Actor"/> (ADR 0019).
/// </summary>
/// <remarks>
/// <para>
/// The caller's subject becomes the user name of the request's Wolverine message context. Wolverine puts it on the
/// envelope of every message the request sends, and the idempotency policy of <c>RimBank.Postgresql</c> scopes
/// command keys by it (ADR 0018, ADR 0019). <c>.Api</c> adapters and cores never see it.
/// </para>
/// <para>
/// A token whose subject or client cannot identify a caller is refused with <c>shared.actor-invalid</c> (401). An
/// anonymous request, which only reaches the health probes, is passed on untouched.
/// </para>
/// </remarks>
internal sealed class ActorRelay(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IProblemDetailsService problemDetails)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var actor = ActorOf(httpContext.User);
            if (actor.IsFailure)
            {
                httpContext.Response.StatusCode = ErrorStatus.For(actor.Error.Kind);
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = ErrorProblemDetails.Create(actor.Error),
                });
                return;
            }

            httpContext.RequestServices.GetRequiredService<IMessageContext>().UserName = actor.Value.Subject;
        }

        await next(httpContext);
    }

    /// <summary>The actor of an authenticated principal: its <c>sub</c> and <c>azp</c> claims.</summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns>The actor, or <c>shared.actor-invalid</c>.</returns>
    internal static Result<Actor> ActorOf(ClaimsPrincipal principal) =>
        Actor.TryCreate(
            principal.FindFirstValue(AuthenticationComposition.SubjectClaim),
            principal.FindFirstValue(AuthenticationComposition.ClientClaim));
}
