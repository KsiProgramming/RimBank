using System.Text.Json.Serialization;
using RimBank.Party.PartyReferenceDataDirectory.BankRelations;
using RimBank.Party.PartyReferenceDataDirectory.Demographics;
using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Reference;

namespace RimBank.Party.PartyReferenceDataDirectory.Retrieval;

/// <summary>
/// The read model of a Party Reference Data Directory entry, served by BIAN
/// <c>PartyReferenceDataDirectoryEntry/Retrieve</c> (ADR 0024). Projected inline from the entry's stream by
/// <see cref="PartyReferenceDataDirectoryEntryViewProjection"/>; it holds no behaviour and never feeds a decision.
/// </summary>
/// <param name="Id">The entry's stream id, the identity of the view's document.</param>
/// <param name="Version">The entry's stream version: the <c>ETag</c> a Retrieve returns and a command sends back in <c>If-Match</c>.</param>
/// <param name="PartyType">Whether the party is a person or an organisation.</param>
/// <param name="OpenDate">The entry's <c>DirectoryEntryDate</c> of type <c>OpenDate</c>.</param>
/// <param name="DemographicsId">The entry's Demographics Behavior Qualifier.</param>
/// <param name="PartyReferenceId">The entry's Reference Behavior Qualifier.</param>
/// <param name="BankRelationsId">The entry's BankRelations Behavior Qualifier.</param>
public sealed record PartyReferenceDataDirectoryEntryView(
    Guid Id,
    long Version,
    PartyType PartyType,
    DateTimeOffset OpenDate,
    DemographicsId DemographicsId,
    PartyReferenceId PartyReferenceId,
    BankRelationsId BankRelationsId)
{
    /// <summary>The entry, as its typed id. Computed from <see cref="Id"/>, so it is not stored.</summary>
    [JsonIgnore]
    public PartyReferenceDataDirectoryEntryId EntryId => PartyReferenceDataDirectoryEntryId.From(Id);
}
