using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>POST …/{partyreferencedatadirectoryid}/Associations/Register</c> and
/// <c>PUT …/Associations/{associationsid}/Update</c>: BIAN's <c>RegisterAssociations</c> and <c>UpdateAssociations</c>, which
/// have the same properties (roadmap 1.11, ADR 0033). Register needs the associate and the type; Update is a partial
/// update in which a value left out keeps its value and an empty validity period clears it. The other BIAN Associations
/// fields are refused; properties that are not BIAN's are ignored.
/// </summary>
public sealed record PartyAssociationRequest
{
    /// <summary>BIAN <c>AssociateReference</c>: the associate's directory entry id in its <c>PartyReference</c>.</summary>
    [JsonPropertyName("AssociateReference")]
    public InvolvedPartyModel? AssociateReference { get; init; }

    /// <summary>
    /// BIAN <c>AssociateType</c>, as a plain string (BIAN's enum wrapper flattened, ADR 0024), for example
    /// <c>PartyIsParentOfParty</c>.
    /// </summary>
    [JsonPropertyName("AssociateType")]
    public string? AssociateType { get; init; }

    /// <summary>BIAN <c>AssociationValidFromToDate</c>: when the association holds.</summary>
    [JsonPropertyName("AssociationValidFromToDate")]
    public AssociationValidityPeriodModel? AssociationValidFromToDate { get; init; }

    /// <summary>Every other property sent: the BIAN Associations fields RimBank does not record are found here.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>
    /// The BIAN paths of the Associations fields this request sends with a value that RimBank does not record: the
    /// top-level fields of <see cref="PartyAssociationFields.NotRecorded"/> (matched without regard to case) and every
    /// other part of the objects it reads. A field sent as <c>null</c> is not sent.
    /// </summary>
    /// <returns>The paths, in the order sent.</returns>
    public IReadOnlyList<string> UnsupportedFields() =>
    [
        .. Present(Other)
            .Select(name => PartyAssociationFields.NotRecorded.FirstOrDefault(field => string.Equals(field, name, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>(),
        .. Present(AssociateReference?.Other).Select(part => $"{PartyAssociationFields.AssociateReference}.{part}"),
        .. Present(AssociationValidFromToDate?.Other).Select(part => $"{PartyAssociationFields.AssociationValidFromToDate}.{part}"),
    ];

    /// <summary>
    /// The Register command this request asks for, <c>party.association-field-not-supported</c> or
    /// <c>party.association-invalid</c>.
    /// </summary>
    /// <param name="ownerId">The entry the route names.</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<RegisterPartyAssociation> ToRegisterCommand(PartyReferenceDataDirectoryEntryId ownerId, IdempotencyKey idempotencyKey) =>
        PartyAssociationTerms.Create(ToInput(), UnsupportedFields())
            .Map(terms => new RegisterPartyAssociation(ownerId, terms, idempotencyKey));

    /// <summary>
    /// The Update command this request asks for, <c>party.association-field-not-supported</c> or
    /// <c>party.association-invalid</c>.
    /// </summary>
    /// <param name="ownerId">The entry the route names.</param>
    /// <param name="associationId">The association the route names.</param>
    /// <param name="expectedVersion">The version from <c>If-Match</c> (ADR 0027).</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<UpdatePartyAssociation> ToUpdateCommand(
        PartyReferenceDataDirectoryEntryId ownerId,
        PartyAssociationId associationId,
        long expectedVersion,
        IdempotencyKey idempotencyKey) =>
        PartyAssociationChanges.Create(ToInput(), UnsupportedFields())
            .Map(changes => new UpdatePartyAssociation(ownerId, associationId, expectedVersion, changes, idempotencyKey));

    private PartyAssociationInput ToInput() =>
        new(
            AssociateReference is { } associate ? associate.PartyReference ?? string.Empty : null,
            AssociateType,
            AssociationValidFromToDate?.ToInput());

    private static IEnumerable<string> Present(Dictionary<string, JsonElement>? properties) =>
        (properties ?? []).Where(property => property.Value.ValueKind != JsonValueKind.Null).Select(property => property.Key);
}
