using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's Reference <c>DocumentDirectoryEntryInstanceReference</c> (a <c>DocumentDirectoryEntry</c>), in requests and
/// responses. RimBank records its <c>DocumentDirectoryEntryIdentification</c> as an opaque reference (roadmap 1.20,
/// ADR 0032); a request that sends any other part of it is refused.
/// </summary>
public sealed record DocumentDirectoryEntryModel
{
    /// <summary>BIAN <c>DocumentDirectoryEntryIdentification</c> (Max35Text).</summary>
    [JsonPropertyName("DocumentDirectoryEntryIdentification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentDirectoryEntryIdentification { get; init; }

    /// <summary>The other parts a request sent (for example <c>DirectoryEntryStatus</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
