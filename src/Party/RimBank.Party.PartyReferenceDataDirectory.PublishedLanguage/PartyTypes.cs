namespace RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

/// <summary>
/// The values of <c>PartyType</c> in a published event (BIAN <c>Partytypevalues</c>). They are strings, not an enum,
/// so a value added later never breaks an older consumer (ADR 0036).
/// </summary>
public static class PartyTypes
{
    /// <summary>A human being.</summary>
    public const string Person = "Person";

    /// <summary>An organised structure: a business, government body, charity or financial institution.</summary>
    public const string Organisation = "Organisation";
}
