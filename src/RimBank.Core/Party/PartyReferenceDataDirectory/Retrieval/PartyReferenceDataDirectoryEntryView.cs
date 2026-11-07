using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

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
/// <param name="RefreshDate">The entry's <c>DirectoryEntryDate</c> of type <c>RefreshDate</c>: its last Update, if any.</param>
/// <param name="Demographics">
/// The entry's Demographics section, served by BIAN <c>Demographics/Retrieve</c>, or <c>null</c> until its first Update
/// (also in documents projected before roadmap 1.9); see <see cref="CurrentDemographics"/>.
/// </param>
public sealed record PartyReferenceDataDirectoryEntryView(
    Guid Id,
    long Version,
    PartyType PartyType,
    DateTimeOffset OpenDate,
    DemographicsId DemographicsId,
    PartyReferenceId PartyReferenceId,
    BankRelationsId BankRelationsId,
    DateTimeOffset? RefreshDate = null,
    DemographicsRecord? Demographics = null)
{
    /// <summary>The entry's Demographics section: empty and unverified until its first Update. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public DemographicsRecord CurrentDemographics => Demographics ?? DemographicsRecord.Empty;

    /// <summary>The entry, as its typed id. Computed from <see cref="Id"/>, so it is not stored.</summary>
    [JsonIgnore]
    public PartyReferenceDataDirectoryEntryId EntryId => PartyReferenceDataDirectoryEntryId.From(Id);
}
