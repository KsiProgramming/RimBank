using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's Reference <c>ResidencyStatus</c> (ISO 20022 <c>Status</c>), in requests and responses. RimBank records the code in
/// <c>StatusDescription</c> and the time it was assigned in <c>StatusDateTime</c> (roadmap 1.18, ADR 0029); a request that
/// sends any other part of it is refused.
/// </summary>
public sealed record ResidencyStatusModel
{
    /// <summary>BIAN <c>StatusDescription</c>: <c>Resident</c> or <c>NonResident</c>.</summary>
    [JsonPropertyName("StatusDescription")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusDescription { get; init; }

    /// <summary>BIAN <c>StatusDateTime</c>: when the status was assigned, an ISO 8601 date-time.</summary>
    [JsonPropertyName("StatusDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusDateTime { get; init; }

    /// <summary>The other parts a request sent (for example <c>StatusReason</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
