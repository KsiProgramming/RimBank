using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's Reference <c>GovernmentIssuedIdentityReference</c> (ISO 20022 <c>PartyIdentificationInformation</c>), in
/// requests and responses. RimBank records the tax identification number and the national registration number
/// (ADR 0029); a request that sends any other part of it is refused.
/// </summary>
public sealed record GovernmentIssuedIdentityModel
{
    /// <summary>BIAN <c>TaxIdentificationNumber</c> (Max35Text).</summary>
    [JsonPropertyName("TaxIdentificationNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TaxIdentificationNumber { get; init; }

    /// <summary>BIAN <c>NationalRegistrationNumber</c> (Max35Text).</summary>
    [JsonPropertyName("NationalRegistrationNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NationalRegistrationNumber { get; init; }

    /// <summary>The other parts a request sent (for example <c>OtherIdentification</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
