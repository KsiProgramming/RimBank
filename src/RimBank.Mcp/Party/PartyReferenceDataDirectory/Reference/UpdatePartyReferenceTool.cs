using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_update_reference</c>: BIAN <c>Reference/Update</c>, a command (ADR 0024, ADR 0029).
/// It dispatches <see cref="UpdatePartyReference"/>, as <c>PUT …/Reference/{referenceid}/Update</c> does, with
/// <c>expectedVersion</c> in place of <c>If-Match</c>, and answers from its acknowledgement (ADR 0039, ADR 0040).
/// </summary>
[McpServerToolType]
public sealed class UpdatePartyReferenceTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_update_reference";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Reference/Update";

    /// <summary>Updates the party's Reference, as the HTTP endpoint does: the same command, idempotency and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="referenceId">The entry's ReferenceId.</param>
    /// <param name="request">The BIAN Update body.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The Reference with the entry's new <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Update a party's reference", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: update details about the customer's reference entry. request is the BIAN "
        + "UpdateReference body, a partial update: GovernmentIssuedIdentityReference.TaxIdentificationNumber and "
        + "NationalRegistrationNumber, the LEI (ISO 17442, check digits verified) in PartyLegalEntityReference.Identification, "
        + "PartyFullLegalName (at most 140 characters), PartyNameSalutation (at most 35), DateofBirth (an ISO 8601 date, "
        + "from 1900-01-01 to today), Nationality.Code and PartyCountryOfResidence.Code (ISO 3166-1 alpha-2), and "
        + "ResidencyStatus (StatusDescription Resident or NonResident, and an optional StatusDateTime). ResidentialAddress, "
        + "PartyAddress and CorporateAddress are ISO 20022 postal addresses: Department, SubDepartment, StreetName, "
        + "StreetBuildingIdentification, BuildingName, Floor, PostOfficeBox, PostCodeIdentification, TownName and "
        + "Country.Code, with TownName and Country required. eMailAddress.EmailAddress, CellPhoneNumber.MobileNumber (E.164, "
        + "for example +33612345678) and SocialNetworkContacts.URLAddress (an https URL) are the contact points. The party's "
        + "declared legal standing is PartyLegalStructure (the BIAN code list, for example PrivateLimitedCompany or "
        + "Co-Operative), PoliticalExposureType (Politicalexposureforeign, Politicalexposuredomestic or "
        + "Nopoliticalexposure), PoliticalExposureDescriptionRecord (at most 350 characters, only with a foreign or domestic "
        + "exposure) and CustomerSinceDate (an ISO 8601 date, not before DateofBirth). GovernmentIssuedDocumentDetails "
        + "records PartyDocumentType and, in DocumentReference, DocumentIdentification.IdentifierValue (both required), "
        + "DocumentName and DocumentValidityPeriod (FromDateTime and ToDateTime DateTimeContent dates); "
        + "DocumentDirectoryEntryInstanceReference records DocumentDirectoryEntryIdentification. On an organisation only, "
        + "CorporateCustomerReference and CorporateCustomerLegalEntityReference name registered organisations and "
        + "CompanyOfficerReference a registered person, each in its PartyReference, with CompanyOfficerRole as text. A "
        + "value left out keeps its value, an empty one clears it, and a given address or document replaces the whole of "
        + "it. Other parts of these objects are refused with party.reference-field-not-supported. Send the version you "
        + "last read as expectedVersion: one that is no longer current is a conflict. The answer's version is the new one.")]
    public static async Task<CallToolResult> UpdateAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The entry's ReferenceId, from Register or Retrieve.")] string referenceId,
        [Description("The BIAN UpdateReference body, with BIAN's property names.")] UpdatePartyReferenceRequest request,
        [Description("The entry's version from the last Retrieve or command (the ETag on HTTP).")] long expectedVersion,
        [Description("A key of 1 to 255 visible ASCII characters, unique per update. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bus);

        var command = ReferenceIds.Read(partyReferenceDataDirectoryEntryId, referenceId)
            .Bind(ids => IdempotencyKey.TryCreate(idempotencyKey)
                .Bind(key => request.ToCommand(ids.EntryId, ids.PartyReferenceId, expectedVersion, key)));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<UpdatePartyReferenceAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(PartyReferenceResult.From(acknowledged)));
    }
}
