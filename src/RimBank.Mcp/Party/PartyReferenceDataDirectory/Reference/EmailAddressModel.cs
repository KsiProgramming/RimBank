using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// BIAN's Reference <c>eMailAddress</c> (a <c>ContactPoint</c>), in requests and responses. RimBank reads the address from
/// the ISO 20022 <c>ElectronicAddress.EmailAddress</c> part (roadmap 1.19, ADR 0031); a request that sends any other part
/// of it is refused.
/// </summary>
public sealed record EmailAddressModel
{
    /// <summary>ISO 20022 <c>EmailAddress</c>: the email address, for example <c>name@example.com</c>.</summary>
    [JsonPropertyName("EmailAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmailAddress { get; init; }

    /// <summary>The other parts a request sent (for example <c>Identification</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
