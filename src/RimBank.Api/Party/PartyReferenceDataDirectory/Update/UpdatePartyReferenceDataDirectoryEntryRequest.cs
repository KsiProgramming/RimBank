using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Update</c>: BIAN's
/// <c>UpdatePartyReferenceDataDirectoryEntry</c> with its property names (ADR 0024). Only <see cref="PartyType"/> is
/// used, and it is optional. The other BIAN fields are accepted and ignored, as in Register: the directory sets the
/// entry's dates itself, and the party's details are recorded by the Demographics and Reference updates.
/// </summary>
public sealed record UpdatePartyReferenceDataDirectoryEntryRequest
{
    /// <summary>BIAN <c>PartyReference</c>. Accepted and ignored: the party's details are Behavior Qualifier data.</summary>
    [JsonPropertyName("PartyReference")]
    public JsonElement? PartyReference { get; init; }

    /// <summary>BIAN <c>DirectoryEntryDateType</c>. Accepted and ignored: Update always records a <c>RefreshDate</c>.</summary>
    [JsonPropertyName("DirectoryEntryDateType")]
    public JsonElement? DirectoryEntryDateType { get; init; }

    /// <summary>BIAN <c>DirectoryEntryDate</c>. Accepted and ignored: the refresh date is the time the directory updates the entry.</summary>
    [JsonPropertyName("DirectoryEntryDate")]
    public JsonElement? DirectoryEntryDate { get; init; }

    /// <summary>BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c> (case-sensitive), or absent to keep the current one.</summary>
    [JsonPropertyName("PartyType")]
    public string? PartyType { get; init; }

    /// <summary>The command this request asks for, or <c>party.party-type-invalid</c> for a party type that is not a BIAN one.</summary>
    /// <param name="entryId">The entry the route names.</param>
    /// <param name="expectedVersion">The version from <c>If-Match</c> (ADR 0027).</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
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
