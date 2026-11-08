using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Retrieve the party's BankRelations: BIAN <c>BankRelations/Retrieve</c> (ADR 0024, ADR 0035). Handled by
/// <see cref="RetrieveBankRelationsHandler"/>, which reads the entry's view; adapters show its BankRelations section.
/// </summary>
/// <param name="EntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> route value.</param>
/// <param name="BankRelationsId">The entry's BankRelations: the <c>{bankrelationsid}</c> route value.</param>
public sealed record RetrieveBankRelations(PartyReferenceDataDirectoryEntryId EntryId, BankRelationsId BankRelationsId)
    : IQuery<PartyReferenceDataDirectoryEntryView>;
