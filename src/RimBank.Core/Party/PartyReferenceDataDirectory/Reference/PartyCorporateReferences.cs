using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The corporate customer's references to other parties of this directory, held by the entry's Reference Behavior
/// Qualifier (roadmap 1.21, ADR 0034). Each reference is the id of a registered directory entry, checked on the write side
/// when it is recorded: the corporate customer and its legal entity are organisations, and the officer is a person. They
/// are recorded only on an organisation's entry. BIAN gives one object per field, so there is one officer. Each part is
/// optional: an unknown one is <c>null</c>.
/// </summary>
/// <param name="CorporateCustomer">BIAN <c>CorporateCustomerReference.PartyReference</c>: the company.</param>
/// <param name="LegalEntity">BIAN <c>CorporateCustomerLegalEntityReference.PartyReference</c>: the legal entity.</param>
/// <param name="CompanyOfficer">BIAN <c>CompanyOfficerReference.PartyReference</c>: the identified company officer.</param>
/// <param name="CompanyOfficerRole">BIAN <c>CompanyOfficerRole</c>: the officer's role, only with an officer.</param>
public sealed record PartyCorporateReferences(
    PartyReferenceDataDirectoryEntryId? CorporateCustomer,
    PartyReferenceDataDirectoryEntryId? LegalEntity,
    PartyReferenceDataDirectoryEntryId? CompanyOfficer,
    CompanyOfficerRole? CompanyOfficerRole)
{
    /// <summary>No reference recorded.</summary>
    public static PartyCorporateReferences Empty { get; } = new(null, null, null, null);

    /// <summary>Whether any part is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The references after a partial update: a part the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting references.</returns>
    public PartyCorporateReferences Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyCorporateReferences(
            changes.CorporateCustomer.ApplyTo(CorporateCustomer),
            changes.LegalEntity.ApplyTo(LegalEntity),
            changes.CompanyOfficer.ApplyTo(CompanyOfficer),
            changes.CompanyOfficerRole.ApplyTo(CompanyOfficerRole));
    }
}
