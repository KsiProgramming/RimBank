namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The acknowledgement of <see cref="UpdateDemographics"/>: the entry's Demographics as the Update decision left it,
/// never a read model (tech-stack §5 and §8.1). The idempotency policy stores it and returns it again for a repeated
/// key (ADR 0018).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="Version">The entry's stream version after the Update, which the next command sends back as <c>If-Match</c>.</param>
/// <param name="Demographics">The Demographics record after the Update.</param>
public sealed record UpdateDemographicsAcknowledged(
    PartyReferenceDataDirectoryEntryId EntryId,
    DemographicsId DemographicsId,
    long Version,
    DemographicsRecord Demographics);
