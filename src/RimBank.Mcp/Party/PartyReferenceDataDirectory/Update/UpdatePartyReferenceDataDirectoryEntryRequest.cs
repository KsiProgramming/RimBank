using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The <c>request</c> argument of <c>party_reference_data_directory_update</c>: BIAN's
/// <c>UpdatePartyReferenceDataDirectoryEntry</c> body with its property names, mirroring the HTTP request (a parity test
/// proves both give the same answer, ADR 0039, ADR 0040). <c>PartyType</c> is optional; the other BIAN fields are
/// accepted and ignored, as on HTTP.
/// </summary>
public sealed record UpdatePartyReferenceDataDirectoryEntryRequest
{
    /// <summary>BIAN <c>PartyReference</c>: accepted and ignored.</summary>
    [JsonPropertyName("PartyReference")]
    public JsonElement? PartyReference { get; init; }

    /// <summary>BIAN <c>DirectoryEntryDateType</c>: accepted and ignored.</summary>
    [JsonPropertyName("DirectoryEntryDateType")]
    public JsonElement? DirectoryEntryDateType { get; init; }

    /// <summary>BIAN <c>DirectoryEntryDate</c>: accepted and ignored.</summary>
    [JsonPropertyName("DirectoryEntryDate")]
    public JsonElement? DirectoryEntryDate { get; init; }

    /// <summary>BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c>; left out keeps the current one.</summary>
    [JsonPropertyName("PartyType")]
    public string? PartyType { get; init; }

    /// <summary>The command this request asks for, or the error of an invalid <c>PartyType</c>.</summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<UpdatePartyReferenceDataDirectoryEntry> ToCommand(
        PartyReferenceDataDirectoryEntryId entryId,
        long expectedVersion,
        IdempotencyKey idempotencyKey) =>
        PartyTypes.FromOptionalBianName(PartyType) switch
        {
            null => new UpdatePartyReferenceDataDirectoryEntry(entryId, expectedVersion, null, idempotencyKey),
            var partyType => partyType.Map(type => new UpdatePartyReferenceDataDirectoryEntry(entryId, expectedVersion, type, idempotencyKey)),
        };
}
