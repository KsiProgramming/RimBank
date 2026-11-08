using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's <c>Involvedparty</c> as the Associations <c>AssociateReference</c>, in requests and responses (roadmap 1.11,
/// ADR 0033). RimBank records its <c>PartyReference</c>, the associate's directory entry id; a request that sends any
/// other part of it (such as <c>InvolvementReference</c>) is refused.
/// </summary>
public sealed record InvolvedPartyModel
{
    /// <summary>BIAN <c>PartyReference</c>: the associate's <c>PartyReferenceDataDirectoryEntryId</c>.</summary>
    [JsonPropertyName("PartyReference")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PartyReference { get; init; }

    /// <summary>The other parts a request sent, which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
