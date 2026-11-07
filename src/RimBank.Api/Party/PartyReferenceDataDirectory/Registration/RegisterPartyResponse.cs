using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of a successful Register: BIAN's <c>RegisterPartyReferenceDataDirectoryEntry</c> fields and the ids of the
/// new entry and its Behavior Qualifiers (ADR 0024). It is built from the command's acknowledgement, never from a read
/// model (tech-stack §5). The entry's stream version is sent as the <c>ETag</c> header, not in the body.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The new entry: the <c>{partyreferencedatadirectoryid}</c> of later operations.</param>
/// <param name="PartyType">BIAN <c>PartyType</c>: <c>Person</c> or <c>Organisation</c>.</param>
/// <param name="DirectoryEntryDateType">BIAN <c>DirectoryEntryDateType</c>: always <c>OpenDate</c> on Register.</param>
/// <param name="DirectoryEntryDate">BIAN <c>DirectoryEntryDate</c>: when the directory registered the party.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> of its operations.</param>
/// <param name="ReferenceId">The entry's Reference: the <c>{referenceid}</c> of its operations.</param>
/// <param name="BankRelationsId">The entry's BankRelations: the <c>{bankrelationsid}</c> of its operations.</param>
public sealed record RegisterPartyResponse(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("PartyType")] string PartyType,
    [property: JsonPropertyName("DirectoryEntryDateType")] string DirectoryEntryDateType,
    [property: JsonPropertyName("DirectoryEntryDate")] DateTimeOffset DirectoryEntryDate,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("ReferenceId")] Guid ReferenceId,
    [property: JsonPropertyName("BankRelationsId")] Guid BankRelationsId)
{
    /// <summary>The <c>DirectoryEntryDateType</c> of the date Register records.</summary>
    public const string OpenDate = "OpenDate";

    /// <summary>The response for <paramref name="acknowledged"/>.</summary>
    /// <param name="acknowledged">The outcome of <see cref="RegisterParty"/>.</param>
    /// <returns>The response body.</returns>
    public static RegisterPartyResponse From(RegisterPartyAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);

        return new RegisterPartyResponse(
            acknowledged.EntryId.Value,
            acknowledged.PartyType.ToString(),
            OpenDate,
            acknowledged.OpenDate,
            acknowledged.DemographicsId.Value,
            acknowledged.PartyReferenceId.Value,
            acknowledged.BankRelationsId.Value);
    }
}
