using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>BIAN <c>Employment</c> of the Demographics, mirroring the HTTP model (ADR 0039).</summary>
/// <param name="PartyReference">The employer.</param>
/// <param name="InvolvementReference">The employment's reference.</param>
public sealed record DemographicsEmployment(
    [property: JsonPropertyName("PartyReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyReference,
    [property: JsonPropertyName("InvolvementReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InvolvementReference)
{
    /// <summary>The core's employment.</summary>
    /// <returns>The employment this model describes.</returns>
    public Employment ToEmployment() => new(PartyReference, InvolvementReference);

    /// <summary>The model of <paramref name="employment"/>, or <c>null</c> when none is recorded.</summary>
    /// <param name="employment">The recorded employment.</param>
    /// <returns>The model.</returns>
    public static DemographicsEmployment? From(Employment? employment) =>
        employment switch
        {
            null => null,
            _ => new DemographicsEmployment(employment.PartyReference, employment.InvolvementReference),
        };
}
