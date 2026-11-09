using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// BIAN's Reference <c>CellPhoneNumber</c> (a <c>ContactPoint</c>), in requests and responses. RimBank reads the number
/// from the ISO 20022 <c>PhoneAddress.MobileNumber</c> part (roadmap 1.19, ADR 0031); a request that sends any other part
/// of it (<c>PhoneNumber</c>, <c>FaxNumber</c>, …) is refused.
/// </summary>
public sealed record MobileNumberModel
{
    /// <summary>ISO 20022 <c>MobileNumber</c>: an E.164 number, for example <c>+33612345678</c>.</summary>
    [JsonPropertyName("MobileNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MobileNumber { get; init; }

    /// <summary>The other parts a request sent (for example <c>PhoneNumber</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
