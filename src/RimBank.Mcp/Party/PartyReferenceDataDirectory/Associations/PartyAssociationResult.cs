using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of the Associations tools (roadmap 1.11, ADR 0033): the fields of the HTTP Associations response with
/// BIAN's names, which it mirrors (a parity test proves the JSON is equal, ADR 0039), and the association's
/// <see cref="Version"/>, which HTTP sends as the <c>ETag</c> header. An association is its own aggregate, so this is the
/// version of its own stream, not of the owner's entry (ADR 0024, ADR 0040).
/// </summary>
/// <param name="AssociationsId">The association.</param>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry that owns it.</param>
/// <param name="AssociateReference">BIAN <c>AssociateReference</c>: the associate's entry.</param>
/// <param name="AssociateType">BIAN <c>AssociateType</c>.</param>
/// <param name="AssociationValidFromToDate">BIAN <c>AssociationValidFromToDate</c>, when recorded.</param>
/// <param name="Version">The association's version: the <c>expectedVersion</c> of its next Update (ADR 0027).</param>
public sealed record PartyAssociationResult(
    [property: JsonPropertyName("AssociationsId")] Guid AssociationsId,
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("AssociateReference")] InvolvedPartyModel AssociateReference,
    [property: JsonPropertyName("AssociateType")] string AssociateType,
    [property: JsonPropertyName("AssociationValidFromToDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AssociationValidityPeriodModel? AssociationValidFromToDate,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The result of a Register or an Update, built from its acknowledgement, never from a read model.</summary>
    /// <param name="acknowledged">The outcome of <see cref="RegisterPartyAssociation"/> or <see cref="UpdatePartyAssociation"/>.</param>
    /// <returns>The tool's result.</returns>
    public static PartyAssociationResult From(PartyAssociationAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(
            acknowledged.AssociationId,
            acknowledged.OwnerId,
            acknowledged.AssociateId,
            acknowledged.AssociateType,
            acknowledged.ValidityPeriod,
            acknowledged.Version);
    }

    /// <summary>The result of a Retrieve, built from the association's view.</summary>
    /// <param name="view">The association's read model.</param>
    /// <returns>The tool's result.</returns>
    public static PartyAssociationResult From(PartyAssociationView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.AssociationId, view.OwnerId, view.AssociateId, view.AssociateType, view.ValidityPeriod, view.Version);
    }

    private static PartyAssociationResult From(
        PartyAssociationId associationId,
        PartyReferenceDataDirectoryEntryId ownerId,
        PartyReferenceDataDirectoryEntryId associateId,
        PartyRelationshipType type,
        AssociationValidityPeriod? period,
        long version) =>
        new(
            associationId.Value,
            ownerId.Value,
            new InvolvedPartyModel { PartyReference = associateId.ToString() },
            type.ToBianName(),
            AssociationValidityPeriodModel.From(period),
            version);
}
