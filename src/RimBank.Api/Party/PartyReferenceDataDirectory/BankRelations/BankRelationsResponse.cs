using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The answer of BIAN <c>BankRelations/Update</c> and <c>BankRelations/Retrieve</c> (roadmap 1.12, ADR 0035): the entry's
/// BankRelations, with every unknown value left out.
/// </summary>
/// <param name="BankRelationsId">The entry's BankRelations: the <c>{bankrelationsid}</c> of its operations.</param>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry.</param>
/// <param name="BankRelationType">BIAN <c>BankRelationType</c>.</param>
/// <param name="BusinessUnitEmployeeReference">BIAN <c>BusinessUnitEmployeeReference</c>: the employee's or unit's entry.</param>
/// <param name="BankRelationReference">BIAN <c>BankRelationReference</c>: the relationship's dates and lifecycle status.</param>
public sealed record BankRelationsResponse(
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId,
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("BankRelationType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BankRelationType = null,
    [property: JsonPropertyName("BusinessUnitEmployeeReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    InvolvedPartyModel? BusinessUnitEmployeeReference = null,
    [property: JsonPropertyName("BankRelationReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BankRelationReferenceModel? BankRelationReference = null)
{
    /// <summary>The answer of an Update, built from its acknowledgement, never from a read model (tech-stack §5).</summary>
    /// <param name="acknowledged">The command's acknowledgement.</param>
    /// <returns>The response.</returns>
    public static BankRelationsResponse From(UpdateBankRelationsAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.BankRelationsId, acknowledged.EntryId, acknowledged.BankRelations);
    }

    /// <summary>The answer of a Retrieve, built from the entry's view.</summary>
    /// <param name="view">The entry's view.</param>
    /// <returns>The response.</returns>
    public static BankRelationsResponse From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.BankRelationsId, view.EntryId, view.CurrentBankRelations);
    }

    private static BankRelationsResponse From(
        BankRelationsId bankRelationsId,
        PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryId entryId,
        BankRelationsDetails details) =>
        new(
            bankRelationsId.Value,
            entryId.Value,
            details.Type?.ToBianName(),
            details.BusinessUnitEmployee is { } employee ? new InvolvedPartyModel { PartyReference = employee.ToString() } : null,
            BankRelationReferenceModel.From(details));
}
