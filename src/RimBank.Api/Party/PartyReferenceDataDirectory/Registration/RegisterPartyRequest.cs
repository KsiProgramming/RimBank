using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>POST /PartyReferenceDataDirectory/Register</c>: BIAN's <c>RegisterPartyReferenceDataDirectoryEntry</c>
/// with its property names (ADR 0024). Only <see cref="PartyType"/> is used. The other BIAN fields are accepted and
/// ignored: the directory sets the entry's open date itself, and the party's details are recorded by the Demographics
/// and Reference updates. BIAN's enum wrappers are flattened to strings.
/// </summary>
public sealed record RegisterPartyRequest
{
    /// <summary>BIAN <c>PartyReference</c>. Accepted and ignored: the party's details are Behavior Qualifier data.</summary>
    [JsonPropertyName("PartyReference")]
    public JsonElement? PartyReference { get; init; }

    /// <summary>BIAN <c>DirectoryEntryDateType</c>. Accepted and ignored: Register always records an <c>OpenDate</c>.</summary>
    [JsonPropertyName("DirectoryEntryDateType")]
    public JsonElement? DirectoryEntryDateType { get; init; }

    /// <summary>BIAN <c>DirectoryEntryDate</c>. Accepted and ignored: the open date is the time the directory registers the party.</summary>
    [JsonPropertyName("DirectoryEntryDate")]
    public JsonElement? DirectoryEntryDate { get; init; }

    /// <summary>BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c> (required, case-sensitive).</summary>
    [JsonPropertyName("PartyType")]
    public string? PartyType { get; init; }

    /// <summary>
    /// The command this request asks for, or <c>party.party-type-required</c> / <c>party.party-type-invalid</c>
    /// when <see cref="PartyType"/> is missing or not a BIAN party type.
    /// </summary>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<RegisterParty> ToCommand(IdempotencyKey idempotencyKey) =>
        PartyTypes.FromBianName(PartyType).Map(partyType => new RegisterParty(partyType, idempotencyKey));
}
