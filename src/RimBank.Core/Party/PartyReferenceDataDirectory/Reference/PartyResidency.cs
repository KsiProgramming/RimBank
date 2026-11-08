using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's nationality and residency, held by the entry's Reference Behavior Qualifier (roadmap 1.18, ADR 0029).
/// Each part is optional: an unknown one is <c>null</c>. Only the current values are recorded. Personal data (7.2).
/// </summary>
/// <param name="Nationality">BIAN <c>Nationality.Code</c>: the country of the party's nationality.</param>
/// <param name="CountryOfResidence">
/// BIAN <c>PartyCountryOfResidence.Code</c>: where a person lives, or where a company's affairs are directed.
/// </param>
/// <param name="ResidencyStatus">BIAN <c>ResidencyStatus</c>.</param>
public sealed record PartyResidency(
    CountryCode? Nationality,
    CountryCode? CountryOfResidence,
    ResidencyStatus? ResidencyStatus)
{
    /// <summary>Nothing recorded.</summary>
    public static PartyResidency Empty { get; } = new(null, null, null);

    /// <summary>Whether any part is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The residency after a partial update: a part the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting residency.</returns>
    public PartyResidency Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyResidency(
            changes.Nationality.ApplyTo(Nationality),
            changes.PartyCountryOfResidence.ApplyTo(CountryOfResidence),
            changes.ResidencyStatus.ApplyTo(ResidencyStatus));
    }
}
