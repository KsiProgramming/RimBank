using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Http;
using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Retrieval;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Retrieval;

/// <summary>
/// <c>GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve</c>: BIAN
/// <c>PartyReferenceDataDirectoryEntry/Retrieve</c>, a query (ADR 0024). It dispatches
/// <see cref="RetrievePartyReferenceDataDirectoryEntry"/> and answers only from the entry's view (tech-stack §5).
/// </summary>
public static class RetrievePartyReferenceDataDirectoryEntryEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "PartyReferenceDataDirectoryEntry/Retrieve";

    /// <summary>Maps the endpoint. A query changes nothing, so it needs no <c>Idempotency-Key</c>.</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapRetrievePartyReferenceDataDirectoryEntry(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Route, HandleAsync)
            .WithName(OperationId)
            .WithTags("PartyReferenceDataDirectoryEntry")
            .WithSummary("Retrieve details about a customer directory entry")
            .WithDescription(
                "Returns the party's directory entry and the ids of its Demographics, Reference and BankRelations. The ETag "
                + "header is the entry's version; send it in If-None-Match to get 304 Not Modified while it is current. "
                + "PartyReference is not returned yet: it is recorded by the Demographics and Reference updates.")
            .Produces<RetrievePartyReferenceDataDirectoryEntryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the entry id, dispatches <see cref="RetrievePartyReferenceDataDirectoryEntry"/>, and answers 200 with
    /// the entry and its version as <c>ETag</c>, 304 when <c>If-None-Match</c> already names that version, or the
    /// query's error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-None-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var entryId = PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid);
        if (entryId.IsFailure)
        {
            return RetrievePartyReferenceDataDirectoryEntryRequestErrors.EntryIdInvalid.ToHttpResult();
        }

        var view = await bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrievePartyReferenceDataDirectoryEntry(entryId.Value),
            cancellationToken);
        return view.Match(
            found =>
            {
                StreamVersionTag.SetOn(http.Response, found.Version);
                return StreamVersionTag.IsNotModified(http.Request, found.Version)
                    ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
                    : (IResult)TypedResults.Ok(RetrievePartyReferenceDataDirectoryEntryResponse.From(found));
            },
            error => error.ToHttpResult());
    }
}
