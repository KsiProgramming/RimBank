using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RimBank.Api;
using RimBank.Problems;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// <c>PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Reference/{referenceid}/Update</c>: BIAN
/// <c>Reference/Update</c>, a command (ADR 0024, ADR 0029). It dispatches <see cref="UpdatePartyReference"/> with the
/// entry's version from <c>If-Match</c> (ADR 0027) and answers from its acknowledgement, never from a read model
/// (tech-stack §5).
/// </summary>
public static class UpdatePartyReferenceEndpoint
{
    /// <summary>The BIAN route.</summary>
    public const string Route = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Reference/{referenceid}/Update";

    /// <summary>The BIAN operation id, also the endpoint name.</summary>
    public const string OperationId = "Reference/Update";

    /// <summary>Maps the endpoint, which requires an <c>Idempotency-Key</c> (ADR 0018) and an <c>If-Match</c> header (ADR 0027).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static RouteHandlerBuilder MapUpdatePartyReference(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut(Route, HandleAsync)
            .RequireIdempotencyKey()
            .WithName(OperationId)
            .WithTags("Reference")
            .WithSummary("Update details about a reference entry for the customer")
            .WithDescription(
                "Updates the party's Reference as a partial update: GovernmentIssuedIdentityReference.TaxIdentificationNumber, "
                + "GovernmentIssuedIdentityReference.NationalRegistrationNumber, the LEI (ISO 17442, check digits verified) in "
                + "PartyLegalEntityReference.Identification, PartyFullLegalName (at most 140 characters), PartyNameSalutation "
                + "(at most 35), DateofBirth (an ISO 8601 date, from 1900-01-01 to today), Nationality.Code and "
                + "PartyCountryOfResidence.Code (ISO 3166-1 alpha-2), and ResidencyStatus (StatusDescription Resident or "
                + "NonResident, and an optional StatusDateTime). ResidentialAddress, PartyAddress and CorporateAddress are ISO "
                + "20022 postal addresses: Department, SubDepartment, StreetName, StreetBuildingIdentification, BuildingName, "
                + "Floor, PostOfficeBox, PostCodeIdentification, TownName and Country.Code, with TownName and Country required. "
                + "eMailAddress.EmailAddress, CellPhoneNumber.MobileNumber (E.164, for example +33612345678) and "
                + "SocialNetworkContacts.URLAddress (an https URL) are the contact points. A value left out keeps its value, an "
                + "empty one clears it, and a given address replaces the whole address. The other BIAN Reference fields are "
                + "refused with party.reference-field-not-supported until the roadmap phase that records them (1.20 legal, "
                + "political exposure and corporate details), as are the other parts of the objects above. Send the entry's ETag in If-Match: a missing one is 428, and one "
                + "that is no longer current is 409. The ETag of the answer is the new version.")
            .Produces<PartyReferenceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Validates the ids, <c>If-Match</c> and the request, dispatches <see cref="UpdatePartyReference"/>, and answers 200
    /// with the Reference and the entry's new version as <c>ETag</c>, or the first error as Problem Details.
    /// </summary>
    /// <param name="partyreferencedatadirectoryid">The <c>{partyreferencedatadirectoryid}</c> route value, as sent.</param>
    /// <param name="referenceid">The <c>{referenceid}</c> route value, as sent.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="idempotency">The <c>Idempotency-Key</c> header, already required by the endpoint filter.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="http">The request, to read <c>If-Match</c> and set the <c>ETag</c> header.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandleAsync(
        string partyreferencedatadirectoryid,
        string referenceid,
        UpdatePartyReferenceRequest request,
        IdempotencyKeyHeader idempotency,
        IMessageBus bus,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(http);

        var command = ReferenceRouteValues.Read(partyreferencedatadirectoryid, referenceid)
            .Bind(ids => StreamVersionTag.ReadIfMatch(http.Request)
                .Bind(expectedVersion => request.ToCommand(ids.EntryId, ids.PartyReferenceId, expectedVersion, idempotency.Value)));
        if (command.IsFailure)
        {
            return command.Error.ToHttpResult();
        }

        var outcome = await bus.InvokeAsync<Result<UpdatePartyReferenceAcknowledged>>(command.Value, cancellationToken);
        return outcome.Match<IResult>(
            acknowledged =>
            {
                StreamVersionTag.SetOn(http.Response, acknowledged.Version);
                return TypedResults.Ok(PartyReferenceResponse.From(acknowledged));
            },
            error => error.ToHttpResult());
    }
}
