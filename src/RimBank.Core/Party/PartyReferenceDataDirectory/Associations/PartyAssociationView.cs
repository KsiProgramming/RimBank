using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The read model of a party association, served by BIAN <c>Associations/Retrieve</c> (ADR 0024, roadmap 1.11).
/// Projected inline from the association's stream by <see cref="PartyAssociationViewProjection"/>; it holds no behaviour
/// and never feeds a decision.
/// </summary>
/// <param name="Id">The association's stream id, the identity of the view's document.</param>
/// <param name="Version">The association's stream version: the <c>ETag</c> a Retrieve returns and an Update sends back in <c>If-Match</c>.</param>
/// <param name="OwnerId">The entry that owns the association.</param>
/// <param name="AssociateId">The associated entry.</param>
/// <param name="AssociateType">How the owner relates to the associate.</param>
/// <param name="ValidityPeriod">When the association holds, if known.</param>
public sealed record PartyAssociationView(
    Guid Id,
    long Version,
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyReferenceDataDirectoryEntryId AssociateId,
    PartyRelationshipType AssociateType,
    AssociationValidityPeriod? ValidityPeriod)
{
    /// <summary>The association, as its typed id. Computed from <see cref="Id"/>, so it is not stored.</summary>
    [JsonIgnore]
    public PartyAssociationId AssociationId => PartyAssociationId.From(Id);
}
