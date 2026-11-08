namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The record of the entry's Reference Behavior Qualifier, an entity of the entry (ADR 0024), shared by the entry's state
/// and its view. Roadmap 1.10 records the party identifiers, 1.18 the names, date of birth, nationality and residency;
/// 1.19–1.20 add the other BIAN Reference fields (ADR 0029).
/// </summary>
/// <remarks>
/// The parts added after 1.10 are <c>null</c> when nothing is recorded, so a Reference stored before them (an event or a
/// view document) reads back unchanged. Use <see cref="CurrentNames"/> and <see cref="CurrentResidency"/> to read them.
/// </remarks>
/// <param name="Identifiers">The party's identifiers.</param>
/// <param name="Names">The party's names, or <c>null</c> when none is recorded.</param>
/// <param name="DateOfBirth">BIAN <c>DateofBirth</c>, or <c>null</c> when unknown. Personal data (7.2).</param>
/// <param name="Residency">The party's nationality and residency, or <c>null</c> when none is recorded.</param>
public sealed record PartyReferenceDetails(
    PartyIdentifiers Identifiers,
    PartyNames? Names = null,
    DateOnly? DateOfBirth = null,
    PartyResidency? Residency = null)
{
    /// <summary>The Reference of a newly registered entry: nothing recorded.</summary>
    public static PartyReferenceDetails Empty { get; } = new(PartyIdentifiers.Empty);

    /// <summary>The party's names: empty when none is recorded.</summary>
    public PartyNames CurrentNames => Names ?? PartyNames.Empty;

    /// <summary>The party's nationality and residency: empty when none is recorded.</summary>
    public PartyResidency CurrentResidency => Residency ?? PartyResidency.Empty;

    /// <summary>The Reference after a partial update.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting record.</returns>
    public PartyReferenceDetails Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var names = CurrentNames.Apply(changes);
        var residency = CurrentResidency.Apply(changes);
        return new PartyReferenceDetails(
            Identifiers.Apply(changes),
            names.HasAny ? names : null,
            changes.DateOfBirth.ApplyTo(DateOfBirth),
            residency.HasAny ? residency : null);
    }
}
