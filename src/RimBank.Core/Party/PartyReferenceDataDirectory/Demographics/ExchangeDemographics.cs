using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Verify or reject the party's current demographic details: BIAN <c>Demographics/Exchange</c> (ADR 0028). Handled by
/// <see cref="ExchangeDemographicsHandler"/>, which acknowledges with <see cref="ExchangeDemographicsAcknowledged"/>.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> route value.</param>
/// <param name="ExpectedVersion">
/// The entry's stream version the caller last read (its <c>If-Match</c>, ADR 0027). Any other current version is
/// <c>party.version-conflict</c>.
/// </param>
/// <param name="Action">Whether the bank verifies or rejects the details, read by <see cref="ExchangeActionTypes.FromName"/>.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record ExchangeDemographics(
    PartyReferenceDataDirectoryEntryId EntryId,
    DemographicsId DemographicsId,
    long ExpectedVersion,
    ExchangeActionType Action,
    IdempotencyKey IdempotencyKey) : ICommand;
