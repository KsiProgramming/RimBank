using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Retrieve the party's demographic details: BIAN <c>Demographics/Retrieve</c> (ADR 0024). Handled by
/// <see cref="RetrieveDemographicsHandler"/>, which reads the entry's view; adapters show its Demographics section.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> route value.</param>
public sealed record RetrieveDemographics(PartyReferenceDataDirectoryEntryId EntryId, DemographicsId DemographicsId)
    : IQuery<PartyReferenceDataDirectoryEntryView>;
