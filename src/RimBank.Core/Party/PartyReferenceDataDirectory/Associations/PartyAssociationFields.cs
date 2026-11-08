using System.Collections.Frozen;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The BIAN names of the Associations fields and parameters, used as the keys of field errors so HTTP and MCP name them
/// the same way, and the BIAN Associations fields RimBank does not record (roadmap 1.11, ADR 0033).
/// </summary>
public static class PartyAssociationFields
{
    /// <summary>The BIAN parameter that names an association.</summary>
    public const string AssociationsId = "associationsid";

    /// <summary>BIAN <c>AssociateReference</c>, which names the associate.</summary>
    public const string AssociateReference = "AssociateReference";

    /// <summary>BIAN <c>AssociateReference.PartyReference</c>: the associate's directory entry id.</summary>
    public const string AssociatePartyReference = AssociateReference + ".PartyReference";

    /// <summary>BIAN <c>AssociateType</c>.</summary>
    public const string AssociateType = "AssociateType";

    /// <summary>BIAN <c>AssociationValidFromToDate</c>.</summary>
    public const string AssociationValidFromToDate = "AssociationValidFromToDate";

    /// <summary>The <c>FromDateTime</c> part of a validity period.</summary>
    public const string FromDateTime = "FromDateTime";

    /// <summary>The <c>ToDateTime</c> part of a validity period.</summary>
    public const string ToDateTime = "ToDateTime";

    /// <summary>
    /// The BIAN Associations fields RimBank does not record (ADR 0033). No roadmap phase records them yet: the
    /// employer contact, the obligations, the beneficiaries and the proxy are not modelled, and product agreements belong
    /// to later contexts.
    /// </summary>
    public static FrozenSet<string> NotRecorded { get; } = new[]
    {
        "EmployeeReference",
        "AssociateObligationorDependencyDescription",
        "ProductInstanceReference",
        "PreferredBeneficiary",
        "ProxyRepresentativePowerofAttorneyReference",
        "ProductInstanseInvolvementReference",
    }.ToFrozenSet(StringComparer.Ordinal);
}
