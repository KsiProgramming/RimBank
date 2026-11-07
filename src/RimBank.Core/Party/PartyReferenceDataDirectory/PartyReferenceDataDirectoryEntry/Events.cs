
namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>A domain event of a Party Reference Data Directory entry (ADR 0024). Internal to this context.</summary>
public abstract record PartyReferenceDataDirectoryEntryEvent;

/// <summary>
/// A party was registered in the directory (BIAN <c>PartyReferenceDataDirectoryEntry/Register</c>). It records the ids of
/// the entry's Demographics, Reference and BankRelations, which exist from now on and are empty until their first Update.
/// </summary>
/// <param name="EntryId">The new entry.</param>
/// <param name="PartyType">Whether the party is a person or an organisation.</param>
/// <param name="OpenDate">The entry's <c>DirectoryEntryDate</c> of type <c>OpenDate</c>.</param>
/// <param name="DemographicsId">The entry's Demographics Behavior Qualifier.</param>
/// <param name="PartyReferenceId">The entry's Reference Behavior Qualifier.</param>
/// <param name="BankRelationsId">The entry's BankRelations Behavior Qualifier.</param>
public sealed record PartyRegistered(
    PartyReferenceDataDirectoryEntryId EntryId,
    PartyType PartyType,
    DateTimeOffset OpenDate,
    DemographicsId DemographicsId,
    PartyReferenceId PartyReferenceId,
    BankRelationsId BankRelationsId) : PartyReferenceDataDirectoryEntryEvent;
