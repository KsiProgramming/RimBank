namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The record of the entry's Reference Behavior Qualifier, an entity of the entry (ADR 0024), shared by the entry's state
/// and its view. Roadmap 1.10 records the party identifiers; 1.18–1.20 add the other BIAN Reference fields (ADR 0029).
/// </summary>
/// <param name="Identifiers">The party's identifiers.</param>
public sealed record PartyReferenceDetails(PartyIdentifiers Identifiers)
{
    /// <summary>The Reference of a newly registered entry: nothing recorded.</summary>
    public static PartyReferenceDetails Empty { get; } = new(PartyIdentifiers.Empty);

    /// <summary>The Reference after a partial update.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting record.</returns>
    public PartyReferenceDetails Apply(PartyReferenceChanges changes) =>
        this with { Identifiers = Identifiers.Apply(changes) };
}
