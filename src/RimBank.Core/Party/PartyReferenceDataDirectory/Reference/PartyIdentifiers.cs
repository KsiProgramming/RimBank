namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The identifiers of a party, held by the entry's Reference Behavior Qualifier (ADR 0024, ADR 0029). Each is optional:
/// an unknown identifier is <c>null</c>. They are personal data (7.2).
/// </summary>
/// <param name="TaxIdentificationNumber">The number a tax authority assigned to the party.</param>
/// <param name="NationalRegistrationNumber">The number a national registration authority assigned to the party.</param>
/// <param name="LegalEntityIdentifier">The party's LEI (ISO 17442).</param>
public sealed record PartyIdentifiers(
    TaxIdentificationNumber? TaxIdentificationNumber,
    NationalRegistrationNumber? NationalRegistrationNumber,
    LegalEntityIdentifier? LegalEntityIdentifier)
{
    /// <summary>No identifier recorded.</summary>
    public static PartyIdentifiers Empty { get; } = new(null, null, null);

    /// <summary>Whether any identifier is recorded.</summary>
    public bool HasAny => this != Empty;

    /// <summary>The identifiers after a partial update: an identifier the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting identifiers.</returns>
    public PartyIdentifiers Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyIdentifiers(
            Change(TaxIdentificationNumber, changes.TaxIdentificationNumber),
            Change(NationalRegistrationNumber, changes.NationalRegistrationNumber),
            Change(LegalEntityIdentifier, changes.LegalEntityIdentifier));
    }

    private static TIdentifier? Change<TIdentifier>(TIdentifier? current, PartyIdentifierChange<TIdentifier>? change)
        where TIdentifier : class, IPartyIdentifier<TIdentifier> =>
        change switch
        {
            null => current,
            _ => change.Value,
        };
}
