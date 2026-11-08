namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The names of a party, held by the entry's Reference Behavior Qualifier (roadmap 1.18, ADR 0029). Each is optional:
/// an unknown name is <c>null</c>. Personal data (7.2).
/// </summary>
/// <param name="FullLegalName">BIAN <c>PartyFullLegalName</c>.</param>
/// <param name="Salutation">BIAN <c>PartyNameSalutation</c>.</param>
public sealed record PartyNames(PartyFullLegalName? FullLegalName, PartyNameSalutation? Salutation)
{
    /// <summary>No name recorded.</summary>
    public static PartyNames Empty { get; } = new(null, null);

    /// <summary>Whether any name is recorded.</summary>
    public bool HasAny => this != Empty;

    /// <summary>The names after a partial update: a name the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting names.</returns>
    public PartyNames Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyNames(
            changes.PartyFullLegalName.ApplyTo(FullLegalName),
            changes.PartyNameSalutation.ApplyTo(Salutation));
    }
}
