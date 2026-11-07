using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's Demographics <c>Employment</c> (an <c>Involvedparty</c>), in requests and responses: the party's employer and
/// its role there, as free text (ADR 0028).
/// </summary>
/// <param name="PartyReference">BIAN <c>PartyReference</c>: the employer, for example its company name.</param>
/// <param name="InvolvementReference">BIAN <c>InvolvementReference</c>: the party's role for the employer.</param>
public sealed record DemographicsEmployment(
    [property: JsonPropertyName("PartyReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PartyReference,
    [property: JsonPropertyName("InvolvementReference"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InvolvementReference)
{
    /// <summary>The core's employment.</summary>
    /// <returns>The <see cref="Employment"/> with the same parts.</returns>
    public Employment ToEmployment() => new(PartyReference, InvolvementReference);

    /// <summary>The model of <paramref name="employment"/>, or <c>null</c> when it is unknown.</summary>
    /// <param name="employment">The core's employment.</param>
    /// <returns>The model, or <c>null</c>.</returns>
    public static DemographicsEmployment? From(Employment? employment) =>
        employment switch
        {
            null => null,
            _ => new DemographicsEmployment(employment.PartyReference, employment.InvolvementReference),
        };
}
