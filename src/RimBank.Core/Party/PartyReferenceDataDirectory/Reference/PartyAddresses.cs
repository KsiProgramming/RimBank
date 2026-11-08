using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's postal addresses, held by the entry's Reference Behavior Qualifier (roadmap 1.19, ADR 0031). Each is
/// optional: an unknown one is <c>null</c>. Only the current addresses are recorded. Personal data (7.2).
/// </summary>
/// <param name="ResidentialAddress">BIAN <c>ResidentialAddress</c>: where the party lives.</param>
/// <param name="PartyAddress">BIAN <c>PartyAddress</c>: the party's postal address.</param>
/// <param name="CorporateAddress">BIAN <c>CorporateAddress</c>: the company's registered address.</param>
public sealed record PartyAddresses(
    PostalAddress? ResidentialAddress,
    PostalAddress? PartyAddress,
    PostalAddress? CorporateAddress)
{
    /// <summary>No address recorded.</summary>
    public static PartyAddresses Empty { get; } = new(null, null, null);

    /// <summary>Whether any address is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The addresses after a partial update: a given address replaces the whole address, an omitted one is kept.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting addresses.</returns>
    public PartyAddresses Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyAddresses(
            changes.ResidentialAddress.ApplyTo(ResidentialAddress),
            changes.PartyAddress.ApplyTo(PartyAddress),
            changes.CorporateAddress.ApplyTo(CorporateAddress));
    }
}
