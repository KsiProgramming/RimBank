using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of the BankRelations tools (roadmap 1.12, ADR 0035): the fields of the HTTP BankRelations response with
/// BIAN's names, which it mirrors (a parity test proves the JSON is equal, ADR 0039), with every unknown value left out,
/// and the entry's <see cref="Version"/>, which HTTP sends as the <c>ETag</c> header.
/// </summary>
/// <param name="BankRelationsId">The entry's BankRelations.</param>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry.</param>
/// <param name="BankRelationType">BIAN <c>BankRelationType</c>.</param>
/// <param name="BusinessUnitEmployeeReference">BIAN <c>BusinessUnitEmployeeReference</c>: the employee's or unit's entry.</param>
/// <param name="BankRelationReference">BIAN <c>BankRelationReference</c>: the relationship's dates and lifecycle status.</param>
/// <param name="Version">The entry's version: the <c>expectedVersion</c> of a later command (ADR 0027).</param>
public sealed record BankRelationsResult(
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId,
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("BankRelationType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BankRelationType,
    [property: JsonPropertyName("BusinessUnitEmployeeReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    InvolvedPartyModel? BusinessUnitEmployeeReference,
    [property: JsonPropertyName("BankRelationReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BankRelationReferenceModel? BankRelationReference,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The result of an Update, built from its acknowledgement, never from a read model (tech-stack §5).</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdateBankRelations"/>.</param>
    /// <returns>The tool's result.</returns>
    public static BankRelationsResult From(UpdateBankRelationsAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.BankRelationsId, acknowledged.EntryId, acknowledged.BankRelations, acknowledged.Version);
    }

    /// <summary>The result of a Retrieve, built from the entry's view.</summary>
    /// <param name="view">The entry's read model.</param>
    /// <returns>The tool's result.</returns>
    public static BankRelationsResult From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.BankRelationsId, view.EntryId, view.CurrentBankRelations, view.Version);
    }

    private static BankRelationsResult From(
        BankRelationsId bankRelationsId,
        PartyReferenceDataDirectoryEntryId entryId,
        BankRelationsDetails details,
        long version) =>
        new(
            bankRelationsId.Value,
            entryId.Value,
            details.Type?.ToBianName(),
            details.BusinessUnitEmployee is { } employee ? new InvolvedPartyModel { PartyReference = employee.ToString() } : null,
            BankRelationReferenceModel.From(details),
            version);
}
