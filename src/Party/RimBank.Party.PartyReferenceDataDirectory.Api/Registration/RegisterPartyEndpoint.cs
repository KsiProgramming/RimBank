using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Http;
using RimBank.Party.PartyReferenceDataDirectory.Registration;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Registration;

/// <summary>
/// <c>POST /PartyReferenceDataDirectory/Register</c>: BIAN <c>PartyReferenceDataDirectoryEntry/Register</c>, a command
/// (ADR 0024). It dispatches <see cref="RegisterParty"/> and answers from its acknowledgement, never from a read model
/// (tech-stack §5).
/// </summary>
public static class RegisterPartyEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/Register";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "PartyReferenceDataDirectoryEntry/Register";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> header (ADR 0018).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapRegisterParty(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Route, HandleAsync)
            .RequireIdempotencyKey()
            .WithName(OperationId)
            .WithTags("PartyReferenceDataDirectoryEntry")
            .WithSummary("Register a customer entity in the catalog")
            .WithDescription(
                "Registers a party (Person or Organisation) in the directory, with its Demographics, Reference and BankRelations. "
                + "Only PartyType is used: PartyReference and the directory entry date fields are accepted and ignored, and the "
                + "open date is the time of registration. The ETag header is the entry's version.")
            .Produces<RegisterPartyResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the request, dispatches <see cref="RegisterParty"/>, and answers 200 with the new entry and its version
    /// as <c>ETag</c>, or the command's error as Problem Details.
    /// </summary>
    /// <param name="request">The BIAN Register body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        RegisterPartyRequest request,
        IdempotencyKeyHeader idempotency,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var command = request.ToCommand(idempotency.Value);
        if (command.IsFailure)
        {
            return command.Error.ToHttpResult();
        }

        var outcome = await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(command.Value, cancellationToken);
        return outcome.Match<IResult>(
            acknowledged =>
            {
                StreamVersionTag.SetOn(http.Response, acknowledged.Version);
                return TypedResults.Ok(RegisterPartyResponse.From(acknowledged));
            },
            error => error.ToHttpResult());
    }
}
