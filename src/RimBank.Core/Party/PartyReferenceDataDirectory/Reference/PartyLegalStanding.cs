using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's declared legal standing, held by the entry's Reference Behavior Qualifier (roadmap 1.20, ADR 0032): its
/// legal structure, its political exposure and when it became a customer. Each part is optional: an unknown one is
/// <c>null</c>. These are what the party declares; screening and due-diligence outcomes are not recorded here.
/// </summary>
/// <param name="LegalStructure">BIAN <c>PartyLegalStructure</c>.</param>
/// <param name="PoliticalExposureType">BIAN <c>PoliticalExposureType</c>. Sensitive personal data (7.2).</param>
/// <param name="PoliticalExposureDescription">
/// BIAN <c>PoliticalExposureDescriptionRecord</c>, only with a foreign or domestic exposure. Sensitive personal data (7.2).
/// </param>
/// <param name="CustomerSince">BIAN <c>CustomerSinceDate</c>: when the relationship with the bank started.</param>
public sealed record PartyLegalStanding(
    PartyLegalStructure? LegalStructure,
    PoliticalExposureType? PoliticalExposureType,
    PoliticalExposureDescription? PoliticalExposureDescription,
    DateOnly? CustomerSince)
{
    /// <summary>Nothing recorded.</summary>
    public static PartyLegalStanding Empty { get; } = new(null, null, null, null);

    /// <summary>Whether any part is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The legal standing after a partial update: a part the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting legal standing.</returns>
    public PartyLegalStanding Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyLegalStanding(
            changes.PartyLegalStructure.ApplyTo(LegalStructure),
            changes.PoliticalExposureType.ApplyTo(PoliticalExposureType),
            changes.PoliticalExposureDescription.ApplyTo(PoliticalExposureDescription),
            changes.CustomerSinceDate.ApplyTo(CustomerSince));
    }
}
