namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Where the party's relationship with the bank stands: BIAN <c>BankRelationReference.RelationshipLifecycleStatus</c>, an
/// ISO 20022 <c>Status</c> whose <c>StatusDescription</c> RimBank reads as one of these values (roadmap 1.12, ADR 0035). It
/// is recorded as declared and never derived from the relationship's dates.
/// </summary>
public enum BankRelationLifecycleStatus
{
    /// <summary>The relationship is in force.</summary>
    Active = 1,

    /// <summary>The relationship is on hold.</summary>
    Suspended = 2,

    /// <summary>The relationship has ended.</summary>
    Ended = 3,
}
