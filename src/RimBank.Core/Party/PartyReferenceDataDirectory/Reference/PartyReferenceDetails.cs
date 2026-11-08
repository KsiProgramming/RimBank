using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The record of the entry's Reference Behavior Qualifier, an entity of the entry (ADR 0024), shared by the entry's state
/// and its view. Roadmap 1.10 records the party identifiers, 1.18 the names, date of birth, nationality and residency,
/// 1.19 the postal addresses and contact points, 1.20 the declared legal standing and documents, and 1.21 the corporate
/// customer's references to other parties (ADR 0029, ADR 0032, ADR 0034).
/// </summary>
/// <remarks>
/// The parts added after 1.10 are <c>null</c> when nothing is recorded, so a Reference stored before them (an event or a
/// view document) reads back unchanged. Use <see cref="CurrentNames"/>, <see cref="CurrentResidency"/>,
/// <see cref="CurrentAddresses"/> and <see cref="CurrentContactPoints"/> to read them.
/// </remarks>
/// <param name="Identifiers">The party's identifiers.</param>
/// <param name="Names">The party's names, or <c>null</c> when none is recorded.</param>
/// <param name="DateOfBirth">BIAN <c>DateofBirth</c>, or <c>null</c> when unknown. Personal data (7.2).</param>
/// <param name="Residency">The party's nationality and residency, or <c>null</c> when none is recorded.</param>
/// <param name="Addresses">The party's postal addresses (1.19), or <c>null</c> when none is recorded.</param>
/// <param name="ContactPoints">
/// The party's email, mobile and social network contacts (1.19), or <c>null</c> when none is recorded.
/// </param>
/// <param name="LegalStanding">
/// The party's declared legal structure, political exposure and customer-since date (1.20), or <c>null</c> when none is
/// recorded.
/// </param>
/// <param name="Documents">
/// The party's declared government-issued document and document directory reference (1.20), or <c>null</c> when none is
/// recorded.
/// </param>
/// <param name="CorporateReferences">
/// The corporate customer, legal entity, company officer and officer role (1.21), or <c>null</c> when none is recorded.
/// </param>
public sealed record PartyReferenceDetails(
    PartyIdentifiers Identifiers,
    PartyNames? Names = null,
    DateOnly? DateOfBirth = null,
    PartyResidency? Residency = null,
    PartyAddresses? Addresses = null,
    PartyContactPoints? ContactPoints = null,
    PartyLegalStanding? LegalStanding = null,
    PartyDocuments? Documents = null,
    PartyCorporateReferences? CorporateReferences = null)
{
    /// <summary>The corporate customer's references: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyCorporateReferences CurrentCorporateReferences => CorporateReferences ?? PartyCorporateReferences.Empty;

    /// <summary>The party's declared legal standing: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyLegalStanding CurrentLegalStanding => LegalStanding ?? PartyLegalStanding.Empty;

    /// <summary>The party's declared documents: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyDocuments CurrentDocuments => Documents ?? PartyDocuments.Empty;

    /// <summary>The Reference of a newly registered entry: nothing recorded.</summary>
    public static PartyReferenceDetails Empty { get; } = new(PartyIdentifiers.Empty);

    /// <summary>The party's names: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyNames CurrentNames => Names ?? PartyNames.Empty;

    /// <summary>The party's nationality and residency: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyResidency CurrentResidency => Residency ?? PartyResidency.Empty;

    /// <summary>The party's postal addresses: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyAddresses CurrentAddresses => Addresses ?? PartyAddresses.Empty;

    /// <summary>The party's contact points: empty when none is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public PartyContactPoints CurrentContactPoints => ContactPoints ?? PartyContactPoints.Empty;

    /// <summary>The Reference after a partial update.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting record.</returns>
    public PartyReferenceDetails Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var names = CurrentNames.Apply(changes);
        var residency = CurrentResidency.Apply(changes);
        var addresses = CurrentAddresses.Apply(changes);
        var contactPoints = CurrentContactPoints.Apply(changes);
        var legalStanding = CurrentLegalStanding.Apply(changes);
        var documents = CurrentDocuments.Apply(changes);
        var corporateReferences = CurrentCorporateReferences.Apply(changes);
        return new PartyReferenceDetails(
            Identifiers.Apply(changes),
            names.HasAny ? names : null,
            changes.DateOfBirth.ApplyTo(DateOfBirth),
            residency.HasAny ? residency : null,
            addresses.HasAny ? addresses : null,
            contactPoints.HasAny ? contactPoints : null,
            legalStanding.HasAny ? legalStanding : null,
            documents.HasAny ? documents : null,
            corporateReferences.HasAny ? corporateReferences : null);
    }
}
