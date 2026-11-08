namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// How the owner of an association relates to its associate: BIAN <c>Party_Partyrelationshiptypevalues</c> (roadmap
/// 1.11, ADR 0033). A record reads "the owner <em>type</em> the associate": <see cref="PartyIsParentOfParty"/> means the
/// owner is the associate's parent. The member names are the BIAN names, and are what the store keeps.
/// </summary>
public enum PartyRelationshipType
{
    /// <summary>The owner is the parent of the associate.</summary>
    PartyIsParentOfParty,

    /// <summary>The owner is a subsidiary of the associate.</summary>
    PartyIsSubsidiaryOfParty,

    /// <summary>The owner is certified by the associate.</summary>
    PartyIsCertifiedByParty,

    /// <summary>The owner is married to the associate.</summary>
    PartyIsMarriedToParty,

    /// <summary>The owner acts on behalf of the associate.</summary>
    PartyActsOnBehalfOfParty,

    /// <summary>The owner is a contact point for the associate.</summary>
    PartyIsContactPointForParty,

    /// <summary>The owner is trusted by the associate.</summary>
    PartyIsTrustedByParty,

    /// <summary>The owner holds a power of attorney for the associate. Listed only in BIAN's description (ADR 0033).</summary>
    PartyIsThePowerOfAttorneyForParty,

    /// <summary>The owner receives benefits from the associate. Listed only in BIAN's description (ADR 0033).</summary>
    PartyReceivesBenefitsFromAParty,
}
