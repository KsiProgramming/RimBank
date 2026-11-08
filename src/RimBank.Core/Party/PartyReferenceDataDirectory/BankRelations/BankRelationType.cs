namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// What the bank is to the party: BIAN <c>BankRelationType</c>, "the type of relationship (e.g. relationship manager)"
/// (roadmap 1.12, ADR 0035). BIAN gives no list, so RimBank closes it to these values. Each one says which kind of party
/// <c>BusinessUnitEmployeeReference</c> names (see <see cref="BankRelationTypes.ReferencedPartyType"/>).
/// </summary>
public enum BankRelationType
{
    /// <summary>A bank employee manages the relationship with the party. The reference is a person.</summary>
    RelationshipManager = 1,

    /// <summary>A bank employee is the officer of the party's accounts. The reference is a person.</summary>
    AccountOfficer = 2,

    /// <summary>A branch of the bank services the party. The reference is an organisation.</summary>
    ServicingBranch = 3,

    /// <summary>Another business unit of the bank services the party. The reference is an organisation.</summary>
    ServicingBusinessUnit = 4,
}
