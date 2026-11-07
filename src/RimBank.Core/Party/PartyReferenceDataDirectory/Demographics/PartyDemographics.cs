namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The demographic details of a party, held by the entry's Demographics Behavior Qualifier (ADR 0024). Every field is
/// optional, trimmed, and never blank: an unknown detail is <c>null</c>. BIAN's <c>EthnicityReligion</c> is not
/// modelled, because RimBank keeps no special-category personal data (ADR 0024). Every field is personal data (7.2).
/// </summary>
/// <param name="SocioEconomicClassification">The bank's socio-economic rating and prospect of the party (Max140Text).</param>
/// <param name="Employment">The party's employer, or <c>null</c> when unknown; never an empty <see cref="PartyReferenceDataDirectory.Employment"/>.</param>
/// <param name="EmploymentHistory">The party's employment history (Max2048Text).</param>
/// <param name="EducationHistory">The party's education (Max2048Text).</param>
/// <param name="ServicingConstraints">Known constraints on servicing the party, such as sight or hearing (Max2048Text).</param>
public sealed record PartyDemographics(
    string? SocioEconomicClassification,
    Employment? Employment,
    string? EmploymentHistory,
    string? EducationHistory,
    string? ServicingConstraints)
{
    /// <summary>No details recorded: the Demographics of a newly registered entry.</summary>
    public static PartyDemographics Empty { get; } = new(null, null, null, null, null);

    /// <summary>Whether any detail is recorded.</summary>
    public bool HasDetails => this != Empty;

    /// <summary>
    /// The details after a partial update: a field the changes leave out keeps its value, an empty one clears it, and
    /// a given <see cref="DemographicsChanges.Employment"/> replaces the whole employment.
    /// </summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting details.</returns>
    public PartyDemographics Apply(DemographicsChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyDemographics(
            Change(SocioEconomicClassification, changes.SocioEconomicClassification),
            changes.Employment switch
            {
                null => Employment,
                { IsEmpty: true } => null,
                var employment => employment,
            },
            Change(EmploymentHistory, changes.EmploymentHistory),
            Change(EducationHistory, changes.EducationHistory),
            Change(ServicingConstraints, changes.ServicingConstraints));
    }

    private static string? Change(string? current, string? change) =>
        change switch
        {
            null => current,
            "" => null,
            _ => change,
        };
}
