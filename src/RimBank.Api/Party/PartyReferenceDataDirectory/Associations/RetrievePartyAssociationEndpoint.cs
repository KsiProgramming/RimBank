using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/{associationsid}/Retrieve</c>: BIAN
/// <c>Associations/Retrieve</c>, a query (ADR 0024). It dispatches <see cref="RetrievePartyAssociation"/> and answers
/// only from the association's view (tech-stack §5).
/// </summary>
public static class RetrievePartyAssociationEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/{associationsid}/Retrieve";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "Associations/Retrieve";

    /// <summary>Maps the endpoint. A query changes nothing, so it needs no <c>Idempotency-Key</c>.</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapRetrievePartyAssociation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Route, HandleAsync)
            .RequireOperation(OperationId)
            .WithTags("Associations")
            .WithSummary("Retrieve details about association entries in the directory record")
            .WithDescription(
                "Returns the association: AssociateReference.PartyReference (the associate's entry id), AssociateType and, "
                + "when recorded, AssociationValidFromToDate in UTC. Only the entry that owns the association finds it. The "
                + "ETag header is the association's version; send it in If-None-Match to get 304 Not Modified while it is "
                + "current.")
            .Produces<PartyAssociationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the ids, dispatches <see cref="RetrievePartyAssociation"/>, and answers 200 with the association and its
    /// version as <c>ETag</c>, 304 when <c>If-None-Match</c> already names that version, or the query's error as Problem
    /// Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="associationsid">The <c>{associationsid}</c> route value, as sent.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-None-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        string associationsid,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var ids = AssociationRouteValues.Read(partyreferencedatadirectoryid, associationsid);
        if (ids.IsFailure)
        {
            return ids.Error.ToHttpResult();
        }

        var view = await bus.InvokeAsync<Result<PartyAssociationView>>(
            new RetrievePartyAssociation(ids.Value.OwnerId, ids.Value.AssociationId),
            cancellationToken);
        return view.Match(
            found =>
            {
                StreamVersionTag.SetOn(http.Response, found.Version);
                return StreamVersionTag.IsNotModified(http.Request, found.Version)
                    ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
                    : (IResult)TypedResults.Ok(PartyAssociationResponse.From(found));
            },
            error => error.ToHttpResult());
    }
}
