using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// BIAN's Reference <c>PartyLegalEntityReference</c> (ISO 20022 <c>Party</c>), in requests and responses. RimBank keeps
/// the party's LEI in its <c>Identification</c> (ADR 0029); a request that sends any other part of it is refused.
/// </summary>
public sealed record PartyLegalEntityModel
{
    /// <summary>BIAN <c>Identification</c>: the party's LEI (ISO 17442).</summary>
    [JsonPropertyName("Identification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Identification { get; init; }

    /// <summary>The other parts a request sent (for example <c>ContactPoint</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
