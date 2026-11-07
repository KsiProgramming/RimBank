namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's employer, as BIAN's Demographics <c>Employment</c> (<c>Involvedparty</c>) names it. Both parts are free
/// text: the employer need not be a party of this directory, and RimBank does not check it (ADR 0028).
/// </summary>
/// <param name="PartyReference">The employer, for example its company name (Max140Text).</param>
/// <param name="InvolvementReference">The role the party plays for the employer, for example its job position (Max140Text).</param>
public sealed record Employment(string? PartyReference, string? InvolvementReference)
{
    /// <summary>Whether neither part is known.</summary>
    public bool IsEmpty => PartyReference is null && InvolvementReference is null;
}
