namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The entry's demographic details were updated (BIAN <c>Demographics/Update</c>). It carries the full details and the
/// verification the entry's Demographics has from now on: changed details are unverified, unchanged ones keep their
/// verification (ADR 0028).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="Details">The party's demographic details after the update.</param>
/// <param name="VerificationStatus">The verification of those details.</param>
/// <param name="VerificationDate">When they were last verified or rejected, if they still are.</param>
public sealed record DemographicsUpdated(
    PartyReferenceDataDirectoryEntryId EntryId,
    DemographicsId DemographicsId,
    PartyDemographics Details,
    DemographicsVerificationStatus VerificationStatus,
    DateTimeOffset? VerificationDate) : PartyReferenceDataDirectoryEntryEvent;

/// <summary>
/// The bank verified or rejected the entry's current demographic details (BIAN <c>Demographics/Exchange</c>, ADR 0028).
/// </summary>
/// <param name="EntryId">The entry.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="VerificationStatus">The outcome: <see cref="DemographicsVerificationStatus.Verified"/> or <see cref="DemographicsVerificationStatus.Rejected"/>.</param>
/// <param name="VerificationDate">When the bank verified or rejected them.</param>
public sealed record DemographicsExchanged(
    PartyReferenceDataDirectoryEntryId EntryId,
    DemographicsId DemographicsId,
    DemographicsVerificationStatus VerificationStatus,
    DateTimeOffset VerificationDate) : PartyReferenceDataDirectoryEntryEvent;
