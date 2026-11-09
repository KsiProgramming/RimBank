using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/BankRelations/{bankrelationsid}/Update</c>: BIAN
/// <c>BankRelations/Update</c>, a command (ADR 0024, ADR 0035). It dispatches <see cref="UpdateBankRelations"/> with the
/// entry's version from <c>If-Match</c> (ADR 0027) and answers from its acknowledgement, never from a read model
/// (tech-stack §5).
/// </summary>
public static class UpdateBankRelationsEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/BankRelations/{bankrelationsid}/Update";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "BankRelations/Update";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> (ADR 0018) and an <c>If-Match</c> header (ADR 0027).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapUpdateBankRelations(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut(Route, HandleAsync)
            .RequireIdempotencyKey()
            .RequireOperation(OperationId)
            .WithTags("BankRelations")
            .WithSummary("Update details about a customer's bank relations entry for the customer")
            .WithDescription(
                "Updates the party's relationship with the bank as a partial update. BankRelationType is RelationshipManager "
                + "or AccountOfficer (BusinessUnitEmployeeReference.PartyReference then names a registered person) or "
                + "ServicingBranch or ServicingBusinessUnit (it then names a registered organisation); the reference must be "
                + "another entry of the directory and needs a type. BankRelationReference records RelationshipStartDate and "
                + "RelationshipEndDate (DateTimeContent, an ISO 8601 date or date-time, the end not before the start) and "
                + "RelationshipLifecycleStatus.StatusDescription (Active, Suspended or Ended). A value left out keeps its value, "
                + "an empty one clears it, and an empty BankRelationReference clears the dates and the status. Every other "
                + "part (InvolvementReference, RelationshipType, RelationshipValidityPeriod, RelationshipIdentification, the "
                + "other Datetime and Status parts) is refused with party.bank-relations-field-not-supported. Send the entry's "
                + "ETag in If-Match: a missing one is 428, and one that is no longer current is 409. The ETag of the answer is "
                + "the new version.")
            .Produces<BankRelationsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the ids, <c>If-Match</c> and the request, dispatches <see cref="UpdateBankRelations"/>, and answers 200
    /// with the BankRelations and the entry's new version as <c>ETag</c>, or the first error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="bankrelationsid">The <c>{bankrelationsid}</c> route value, as sent.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        string bankrelationsid,
        UpdateBankRelationsRequest request,
        IdempotencyKeyHeader idempotency,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var command = BankRelationsRouteValues.Read(partyreferencedatadirectoryid, bankrelationsid)
            .Bind(ids => StreamVersionTag.ReadIfMatch(http.Request)
                .Bind(expectedVersion => request.ToCommand(ids.EntryId, ids.BankRelationsId, expectedVersion, idempotency.Value)));
        if (command.IsFailure)
        {
            return command.Error.ToHttpResult();
        }

        var outcome = await bus.InvokeAsync<Result<UpdateBankRelationsAcknowledged>>(command.Value, cancellationToken);
        return outcome.Match<IResult>(
            acknowledged =>
            {
                StreamVersionTag.SetOn(http.Response, acknowledged.Version);
                return TypedResults.Ok(BankRelationsResponse.From(acknowledged));
            },
            error => error.ToHttpResult());
    }
}
