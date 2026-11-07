using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Update</c>: BIAN
/// <c>PartyReferenceDataDirectoryEntry/Update</c>, a command (ADR 0024). It dispatches
/// <see cref="UpdatePartyReferenceDataDirectoryEntry"/> with the version from <c>If-Match</c> (ADR 0027) and answers
/// from its acknowledgement, never from a read model (tech-stack §5).
/// </summary>
public static class UpdatePartyReferenceDataDirectoryEntryEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Update";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "PartyReferenceDataDirectoryEntry/Update";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> (ADR 0018) and an <c>If-Match</c> header (ADR 0027).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapUpdatePartyReferenceDataDirectoryEntry(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut(Route, HandleAsync)
            .RequireIdempotencyKey()
            .WithName(OperationId)
            .WithTags("PartyReferenceDataDirectoryEntry")
            .WithSummary("Update details about the customer's directory entry")
            .WithDescription(
                "Updates the party's directory entry and records the time of the update as its RefreshDate. PartyType is "
                + "optional: leave it out to keep the current one. PartyReference and the directory entry date fields are "
                + "accepted and ignored. Send the entry's ETag in If-Match: a missing one is 428, and one that is no longer "
                + "current is 409, so retrieve the entry again and retry. The ETag header of the answer is the new version.")
            .Produces<UpdatePartyReferenceDataDirectoryEntryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the entry id, <c>If-Match</c> and the request, dispatches
    /// <see cref="UpdatePartyReferenceDataDirectoryEntry"/>, and answers 200 with the updated entry and its new version as
    /// <c>ETag</c>, or the first error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        UpdatePartyReferenceDataDirectoryEntryRequest request,
        IdempotencyKeyHeader idempotency,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var entryId = PartyReferenceDataDirectoryEntryId.TryCreate(partyreferencedatadirectoryid);
        if (entryId.IsFailure)
        {
            return PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid.ToHttpResult();
        }

        var command = StreamVersionTag.ReadIfMatch(http.Request)
            .Bind(expectedVersion => request.ToCommand(entryId.Value, expectedVersion, idempotency.Value));
        if (command.IsFailure)
        {
            return command.Error.ToHttpResult();
        }

        var outcome = await bus.InvokeAsync<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>>(command.Value, cancellationToken);
        return outcome.Match<IResult>(
            acknowledged =>
            {
                StreamVersionTag.SetOn(http.Response, acknowledged.Version);
                return TypedResults.Ok(UpdatePartyReferenceDataDirectoryEntryResponse.From(acknowledged));
            },
            error => error.ToHttpResult());
    }
}
