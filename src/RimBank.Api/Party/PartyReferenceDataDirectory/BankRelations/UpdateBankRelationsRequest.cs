using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>PUT …/{partyreferencedatadirectoryid}/BankRelations/{bankrelationsid}/Update</c>: BIAN's
/// <c>UpdateBankRelations</c> (roadmap 1.12, ADR 0035), a partial update in which a value left out keeps its value and an
/// empty one clears it. The parts of the BIAN objects RimBank does not record are refused; properties that are not BIAN's
/// are ignored.
/// </summary>
public sealed record UpdateBankRelationsRequest
{
    /// <summary>BIAN <c>BankRelationType</c>, for example <c>RelationshipManager</c>.</summary>
    [JsonPropertyName("BankRelationType")]
    public string? BankRelationType { get; init; }

    /// <summary>BIAN <c>BusinessUnitEmployeeReference</c>: the employee's or unit's directory entry id in its <c>PartyReference</c>.</summary>
    [JsonPropertyName("BusinessUnitEmployeeReference")]
    public InvolvedPartyModel? BusinessUnitEmployeeReference { get; init; }

    /// <summary>BIAN <c>BankRelationReference</c>: the relationship's dates and lifecycle status.</summary>
    [JsonPropertyName("BankRelationReference")]
    public BankRelationReferenceModel? BankRelationReference { get; init; }

    /// <summary>Every other property sent, which is not a BIAN BankRelations field and is ignored.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>The BIAN paths of the parts this request sends with a value that RimBank does not record.</summary>
    /// <returns>The paths, in the order sent.</returns>
    public IReadOnlyList<string> UnsupportedFields() =>
    [
        .. ReferenceModelParts.Present(BankRelationsFields.BusinessUnitEmployeeReference, BusinessUnitEmployeeReference?.Other),
        .. BankRelationReference?.UnsupportedParts() ?? [],
    ];

    /// <summary>
    /// The Update command this request asks for, <c>party.bank-relations-field-not-supported</c> or
    /// <c>party.bank-relations-invalid</c>.
    /// </summary>
    /// <param name="entryId">The entry the route names.</param>
    /// <param name="bankRelationsId">The BankRelations the route names.</param>
    /// <param name="expectedVersion">The version from <c>If-Match</c> (ADR 0027).</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<UpdateBankRelations> ToCommand(
        PartyReferenceDataDirectoryEntryId entryId,
        BankRelationsId bankRelationsId,
        long expectedVersion,
        IdempotencyKey idempotencyKey) =>
        BankRelationsChanges.Create(ToInput(), UnsupportedFields())
            .Map(changes => new UpdateBankRelations(entryId, bankRelationsId, expectedVersion, changes, idempotencyKey));

    private BankRelationsInput ToInput() =>
        new(
            BankRelationType,
            BusinessUnitEmployeeReference is { } employee ? employee.PartyReference ?? string.Empty : null,
            BankRelationReference?.ToInput());
}
