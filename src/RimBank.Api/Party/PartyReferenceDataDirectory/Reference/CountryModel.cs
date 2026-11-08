using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's Reference <c>Country</c> object (<c>Nationality</c>, <c>PartyCountryOfResidence</c>), in requests and responses.
/// RimBank records its ISO 3166 <c>Code</c> (roadmap 1.18, ADR 0029); a request that sends any other part of it is refused.
/// </summary>
public sealed record CountryModel
{
    /// <summary>BIAN <c>Code</c>: the ISO 3166-1 alpha-2 country code, for example <c>FR</c>.</summary>
    [JsonPropertyName("Code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    /// <summary>The other parts a request sent (for example <c>Citizen</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
