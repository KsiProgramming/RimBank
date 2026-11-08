using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body every Associations operation answers with (roadmap 1.11, ADR 0033): BIAN's <c>RetrieveAssociations</c> fields
/// RimBank records, with the ids of the association and of the entry that owns it. Register and Update build it from
/// their acknowledgement, Retrieve from the association's view.
/// </summary>
/// <param name="AssociationsId">The association: the <c>{associationsid}</c> of its Update and Retrieve.</param>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry that owns the association.</param>
/// <param name="AssociateReference">BIAN <c>AssociateReference</c>: the associate's entry id in its <c>PartyReference</c>.</param>
/// <param name="AssociateType">BIAN <c>AssociateType</c>, in BIAN spelling.</param>
/// <param name="AssociationValidFromToDate">BIAN <c>AssociationValidFromToDate</c>, left out when no period is recorded.</param>
public sealed record PartyAssociationResponse(
    [property: JsonPropertyName("AssociationsId")] Guid AssociationsId,
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("AssociateReference")] InvolvedPartyModel AssociateReference,
    [property: JsonPropertyName("AssociateType")] string AssociateType,
    [property: JsonPropertyName("AssociationValidFromToDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AssociationValidityPeriodModel? AssociationValidFromToDate = null)
{
    /// <summary>The answer of a Register or an Update, from its acknowledgement.</summary>
    /// <param name="acknowledged">The acknowledgement.</param>
    /// <returns>The response.</returns>
    public static PartyAssociationResponse From(PartyAssociationAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(
            acknowledged.AssociationId, acknowledged.OwnerId, acknowledged.AssociateId, acknowledged.AssociateType, acknowledged.ValidityPeriod);
    }

    /// <summary>The answer of a Retrieve, from the association's view.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The response.</returns>
    public static PartyAssociationResponse From(PartyAssociationView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.AssociationId, view.OwnerId, view.AssociateId, view.AssociateType, view.ValidityPeriod);
    }

    private static PartyAssociationResponse From(
        PartyAssociationId associationId,
        PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryId ownerId,
        PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryId associateId,
        PartyRelationshipType type,
        AssociationValidityPeriod? period) =>
        new(
            associationId.Value,
            ownerId.Value,
            new InvolvedPartyModel { PartyReference = associateId.ToString() },
            type.ToBianName(),
            AssociationValidityPeriodModel.From(period));
}
