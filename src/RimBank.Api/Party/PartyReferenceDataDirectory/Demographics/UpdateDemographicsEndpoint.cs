using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Update</c>: BIAN
/// <c>Demographics/Update</c>, a command (ADR 0024). It dispatches <see cref="UpdateDemographics"/> with the entry's
/// version from <c>If-Match</c> (ADR 0027) and answers from its acknowledgement, never from a read model (tech-stack §5).
/// </summary>
public static class UpdateDemographicsEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Update";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "Demographics/Update";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> (ADR 0018) and an <c>If-Match</c> header (ADR 0027).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapUpdateDemographics(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut(Route, HandleAsync)
            .RequireIdempotencyKey()
            .RequireOperation(OperationId)
            .WithTags("Demographics")
            .WithSummary("Update details about a customer's demographic entry")
            .WithDescription(
                "Updates the party's demographic details as a partial update: a field left out keeps its value, an empty "
                + "one clears it, and Employment replaces the whole employment. Changed details become Unverified until an "
                + "Exchange verifies them. EthnicityReligion is accepted and never stored. Send the entry's ETag in If-Match: "
                + "a missing one is 428, and one that is no longer current is 409. The ETag of the answer is the new version.")
            .Produces<DemographicsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the ids, <c>If-Match</c> and the request, dispatches <see cref="UpdateDemographics"/>, and answers 200
    /// with the Demographics and the entry's new version as <c>ETag</c>, or the first error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="demographicsid">The <c>{demographicsid}</c> route value, as sent.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        string demographicsid,
        UpdateDemographicsRequest request,
        IdempotencyKeyHeader idempotency,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var command = DemographicsRouteValues.Read(partyreferencedatadirectoryid, demographicsid)
            .Bind(ids => StreamVersionTag.ReadIfMatch(http.Request)
                .Bind(expectedVersion => request.ToCommand(ids.EntryId, ids.DemographicsId, expectedVersion, idempotency.Value)));
        if (command.IsFailure)
        {
            return command.Error.ToHttpResult();
        }

        var outcome = await bus.InvokeAsync<Result<UpdateDemographicsAcknowledged>>(command.Value, cancellationToken);
        return outcome.Match<IResult>(
            acknowledged =>
            {
                StreamVersionTag.SetOn(http.Response, acknowledged.Version);
                return TypedResults.Ok(DemographicsResponse.From(acknowledged));
            },
            error => error.ToHttpResult());
    }
}
