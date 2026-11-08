using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Retrieve the party's Reference: BIAN <c>Reference/Retrieve</c> (ADR 0024). Handled by
/// <see cref="RetrievePartyReferenceHandler"/>, which reads the entry's view; adapters show its Reference section.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="PartyReferenceId">The entry's Reference: the <c>{referenceid}</c> route value.</param>
public sealed record RetrievePartyReference(PartyReferenceDataDirectoryEntryId EntryId, PartyReferenceId PartyReferenceId)
    : IQuery<PartyReferenceDataDirectoryEntryView>;
