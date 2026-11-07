namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The kind of party an entry is about: BIAN <c>Partytypevalues</c> (UK Open Banking <c>OBExternalPartyType1Code</c>,
/// ISO 20022 definitions).
/// </summary>
public enum PartyType
{
    /// <summary>A human, as distinguished from a corporate entity.</summary>
    Person,

    /// <summary>An organised structure set up for a purpose: a business, government body, charity or financial institution.</summary>
    Organisation,
}
