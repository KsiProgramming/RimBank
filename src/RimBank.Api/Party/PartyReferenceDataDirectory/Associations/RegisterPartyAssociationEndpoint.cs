using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>POST /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/Register</c>: BIAN
/// <c>Associations/Register</c>, a command (ADR 0024, ADR 0033). It dispatches <see cref="RegisterPartyAssociation"/> and
/// answers from its acknowledgement, never from a read model (tech-stack §5).
/// </summary>
public static class RegisterPartyAssociationEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/Register";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "Associations/Register";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> header (ADR 0018).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapRegisterPartyAssociation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Route, HandleAsync)
            .RequireIdempotencyKey()
            .RequireOperation(OperationId)
            .WithTags("Associations")
            .WithSummary("Capture Association Details")
            .WithDescription(
                "Associates the party with another party of the directory. AssociateReference.PartyReference (the associate's "
                + "PartyReferenceDataDirectoryEntryId) and AssociateType are required. AssociateType is one of "
                + "PartyIsParentOfParty, PartyIsSubsidiaryOfParty, PartyIsCertifiedByParty, PartyIsMarriedToParty, "
                + "PartyActsOnBehalfOfParty, PartyIsContactPointForParty, PartyIsTrustedByParty, "
                + "PartyIsThePowerOfAttorneyForParty or PartyReceivesBenefitsFromAParty, read as \"this party <type> the "
                + "associate\". AssociationValidFromToDate (FromDateTime and ToDateTime, ISO 8601, from not after to) is "
                + "optional. The associate must be registered and must not be the party itself (422). The other BIAN "
                + "Associations fields are refused with party.association-field-not-supported. The ETag header is the "
                + "association's version.")
            .Produces<PartyAssociationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the id and the request, dispatches <see cref="RegisterPartyAssociation"/>, and answers 200 with the new
    /// association and its version as <c>ETag</c>, or the first error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="request">The BIAN Register body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
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

        var command = AssociationRouteValues.ReadOwner(partyreferencedatadirectoryid)
            .Bind(ownerId => request.ToRegisterCommand(ownerId, idempotency.Value));
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
