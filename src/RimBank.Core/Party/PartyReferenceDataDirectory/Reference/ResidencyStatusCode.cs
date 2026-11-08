namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Whether the party is resident for the bank (roadmap 1.18, ADR 0029): carried in BIAN Reference
/// <c>ResidencyStatus.StatusDescription</c>. Stored by name.
/// </summary>
public enum ResidencyStatusCode
{
    /// <summary>The party is resident.</summary>
    Resident = 1,

    /// <summary>The party is not resident.</summary>
    NonResident = 2,
}
