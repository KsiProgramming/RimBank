using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/{associationsid}/Update</c>: BIAN
/// <c>Associations/Update</c>, a command (ADR 0024, ADR 0033). It dispatches <see cref="UpdatePartyAssociation"/> with
/// the association's version from <c>If-Match</c> (ADR 0027) and answers from its acknowledgement (tech-stack §5).
/// </summary>
public static class UpdatePartyAssociationEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/{associationsid}/Update";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "Associations/Update";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> (ADR 0018) and an <c>If-Match</c> header (ADR 0027).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapUpdatePartyAssociation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut(Route, HandleAsync)
            .RequireIdempotencyKey()
            .RequireOperation(OperationId)
            .WithTags("Associations")
            .WithSummary("Update details about an association entry for the customer")
            .WithDescription(
                "Updates the association as a partial update: AssociateType and AssociationValidFromToDate (FromDateTime and "
                + "ToDateTime, ISO 8601, from not after to). A value left out keeps its value, and an empty validity period "
                + "clears it; the type cannot be cleared. The owner and the associate never change: an "
                + "AssociateReference.PartyReference other than the registered associate is 422 "
                + "party.associate-cannot-change. Send the association's ETag in If-Match: a missing one is 428, and one that "
                + "is no longer current is 409. The ETag of the answer is the new version.")
            .Produces<PartyAssociationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the ids, <c>If-Match</c> and the request, dispatches <see cref="UpdatePartyAssociation"/>, and answers 200
    /// with the association and its new version as <c>ETag</c>, or the first error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="associationsid">The <c>{associationsid}</c> route value, as sent.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        string associationsid,
        PartyAssociationRequest request,
        IdempotencyKeyHeader idempotency,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var command = AssociationRouteValues.Read(partyreferencedatadirectoryid, associationsid)
            .Bind(ids => StreamVersionTag.ReadIfMatch(http.Request)
                .Bind(expectedVersion => request.ToUpdateCommand(ids.OwnerId, ids.AssociationId, expectedVersion, idempotency.Value)));
        if (command.IsFailure)
        {
            return command.Error.ToHttpResult();
        }

        var outcome = await bus.InvokeAsync<Result<PartyAssociationAcknowledged>>(command.Value, cancellationToken);
        return outcome.Match<IResult>(
            acknowledged =>
            {
                StreamVersionTag.SetOn(http.Response, acknowledged.Version);
                return TypedResults.Ok(PartyAssociationResponse.From(acknowledged));
            },
            error => error.ToHttpResult());
    }
}
