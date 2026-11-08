namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The legal standing of the party, as the party declares it: BIAN Reference <c>PartyLegalStructure</c>
/// (<c>Partylegalstructuretypevalues</c>, the UK Open Banking <c>OBExternalLegalStructureType1Code</c>; roadmap 1.20,
/// ADR 0032). Stored by name; <see cref="PartyLegalStructures"/> reads and writes the BIAN spelling.
/// </summary>
public enum PartyLegalStructure
{
    /// <summary>BIAN <c>Individual</c>.</summary>
    Individual = 1,

    /// <summary>BIAN <c>CommunityInterestCompany</c>.</summary>
    CommunityInterestCompany = 2,

    /// <summary>
    /// BIAN <c>CharitableIncorporatedOrganisation</c>: in BIAN's description of the code list, though not in its enum
    /// (ADR 0032).
    /// </summary>
    CharitableIncorporatedOrganisation = 3,

    /// <summary>BIAN <c>Co-Operative</c>.</summary>
    CoOperative = 4,

    /// <summary>BIAN <c>Charity</c>.</summary>
    Charity = 5,

    /// <summary>BIAN <c>GeneralPartnership</c>.</summary>
    GeneralPartnership = 6,

    /// <summary>BIAN <c>LimitedLiabilityPartnership</c>.</summary>
    LimitedLiabilityPartnership = 7,

    /// <summary>BIAN <c>ScottishLimitedPartnership</c>.</summary>
    ScottishLimitedPartnership = 8,

    /// <summary>BIAN <c>LimitedPartnership</c>.</summary>
    LimitedPartnership = 9,

    /// <summary>BIAN <c>PrivateLimitedCompany</c>.</summary>
    PrivateLimitedCompany = 10,

    /// <summary>BIAN <c>PublicLimitedCompany</c>.</summary>
    PublicLimitedCompany = 11,

    /// <summary>BIAN <c>Sole(SoleTrader)</c>.</summary>
    SoleTrader = 12,
}
