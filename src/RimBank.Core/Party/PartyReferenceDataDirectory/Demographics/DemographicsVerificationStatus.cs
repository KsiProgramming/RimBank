namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Whether the bank has checked the party's current demographic details, through BIAN <c>Demographics/Exchange</c>
/// (ADR 0028). Any change of the details makes them <see cref="Unverified"/> again.
/// </summary>
public enum DemographicsVerificationStatus
{
    /// <summary>The current details have not been checked since they last changed.</summary>
    Unverified,

    /// <summary>The bank verified the current details.</summary>
    Verified,

    /// <summary>The bank rejected the current details.</summary>
    Rejected,
}
