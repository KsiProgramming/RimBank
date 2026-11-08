namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Whether the party declares a political exposure (PEP): BIAN Reference <c>PoliticalExposureType</c>
/// (<c>Politicalexposuretypevalues</c>; roadmap 1.20, ADR 0032). This is the party's declaration only: screening it is a
/// KYC assessment the directory does not record. Stored by name; <see cref="PoliticalExposureTypes"/> reads and writes the
/// BIAN spelling. Sensitive personal data (7.2).
/// </summary>
public enum PoliticalExposureType
{
    /// <summary>BIAN <c>Politicalexposureforeign</c>: a foreign political exposure.</summary>
    Foreign = 1,

    /// <summary>BIAN <c>Politicalexposuredomestic</c>: a domestic political exposure.</summary>
    Domestic = 2,

    /// <summary>BIAN <c>Nopoliticalexposure</c>: no political exposure.</summary>
    NoExposure = 3,
}
