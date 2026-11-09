using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// BIAN's Reference <c>SocialNetworkContacts</c> (a <c>ContactPoint</c>), in requests and responses. RimBank reads the
/// profile from the ISO 20022 <c>ElectronicAddress.URLAddress</c> part (roadmap 1.19, ADR 0031); a request that sends any
/// other part of it is refused.
/// </summary>
public sealed record SocialNetworkContactModel
{
    /// <summary>ISO 20022 <c>URLAddress</c>: an absolute https URL, for example <c>https://www.linkedin.com/in/name</c>.</summary>
    [JsonPropertyName("URLAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? URLAddress { get; init; }

    /// <summary>The other parts a request sent (for example <c>BICAddress</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
