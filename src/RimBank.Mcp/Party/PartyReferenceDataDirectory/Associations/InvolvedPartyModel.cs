using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// BIAN's <c>Involvedparty</c>, in requests and responses: the Associations <c>AssociateReference</c> (roadmap 1.11,
/// ADR 0033) and the Reference <c>CorporateCustomerReference</c>, <c>CorporateCustomerLegalEntityReference</c> and
/// <c>CompanyOfficerReference</c> (roadmap 1.21, ADR 0034). RimBank records its <c>PartyReference</c>, the id of a
/// directory entry; a request that sends any other part of it (such as <c>InvolvementReference</c>) is refused.
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
