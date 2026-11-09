using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/BankRelations/{bankrelationsid}/Retrieve</c>: BIAN
/// <c>BankRelations/Retrieve</c>, a query (ADR 0024, ADR 0035). It dispatches <see cref="RetrieveBankRelations"/> and
/// answers only from the entry's view (tech-stack §5).
/// </summary>
public static class RetrieveBankRelationsEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/BankRelations/{bankrelationsid}/Retrieve";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "BankRelations/Retrieve";

    /// <summary>Maps the endpoint. A query changes nothing, so it needs no <c>Idempotency-Key</c>.</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapRetrieveBankRelations(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Route, HandleAsync)
            .RequireOperation(OperationId)
            .WithTags("BankRelations")
            .WithSummary("Retrieve details about customer bank relations entries in the directory")
            .WithDescription(
                "Returns the party's relationship with the bank: BankRelationType, the employee or unit in "
                + "BusinessUnitEmployeeReference.PartyReference, and in BankRelationReference the RelationshipStartDate and "
                + "RelationshipEndDate (UTC date-times) and the RelationshipLifecycleStatus. Unknown values are left out. The "
                + "ETag header is the entry's version; send it in If-None-Match to get 304 Not Modified while it is current.")
            .Produces<BankRelationsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the ids, dispatches <see cref="RetrieveBankRelations"/>, and answers 200 with the BankRelations and the
    /// entry's version as <c>ETag</c>, 304 when <c>If-None-Match</c> already names that version, or the query's error as
    /// Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="bankrelationsid">The <c>{bankrelationsid}</c> route value, as sent.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-None-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        string bankrelationsid,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var ids = BankRelationsRouteValues.Read(partyreferencedatadirectoryid, bankrelationsid);
        if (ids.IsFailure)
        {
            return ids.Error.ToHttpResult();
        }

        var view = await bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrieveBankRelations(ids.Value.EntryId, ids.Value.BankRelationsId),
            cancellationToken);
        return view.Match(
            found =>
            {
                StreamVersionTag.SetOn(http.Response, found.Version);
                return StreamVersionTag.IsNotModified(http.Request, found.Version)
                    ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
                    : (IResult)TypedResults.Ok(BankRelationsResponse.From(found));
            },
            error => error.ToHttpResult());
    }
}
