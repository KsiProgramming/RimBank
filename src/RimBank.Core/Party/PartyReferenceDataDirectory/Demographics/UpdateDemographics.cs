using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Update the party's demographic details: BIAN <c>Demographics/Update</c> (ADR 0024), a partial update. Handled by
/// <see cref="UpdateDemographicsHandler"/>, which acknowledges with <see cref="UpdateDemographicsAcknowledged"/>.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> route value.</param>
/// <param name="ExpectedVersion">
/// The entry's stream version the caller last read (its <c>If-Match</c>, ADR 0027). Any other current version is
/// <c>party.version-conflict</c>.
/// </param>
/// <param name="Changes">The changes, built by <see cref="DemographicsChanges.Create"/>.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record UpdateDemographics(
    PartyReferenceDataDirectoryEntryId EntryId,
    DemographicsId DemographicsId,
    long ExpectedVersion,
    DemographicsChanges Changes,
    IdempotencyKey IdempotencyKey) : ICommand;
