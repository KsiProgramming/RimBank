using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Retrieve details about a party's directory entry: BIAN <c>PartyReferenceDataDirectoryEntry/Retrieve</c> (ADR 0024).
/// Handled by <see cref="RetrievePartyReferenceDataDirectoryEntryHandler"/>, which reads the entry's view.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
public sealed record RetrievePartyReferenceDataDirectoryEntry(PartyReferenceDataDirectoryEntryId EntryId)
    : IQuery<PartyReferenceDataDirectoryEntryView>;
