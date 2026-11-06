using RimBank.Party.PartyReferenceDataDirectory.BankRelations;
using RimBank.Party.PartyReferenceDataDirectory.Demographics;
using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Reference;

namespace RimBank.Party.PartyReferenceDataDirectory.Registration;

/// <summary>
/// The acknowledgement of <see cref="RegisterParty"/>: the entry as the Register decision produced it, never a read
/// model (tech-stack §5 and §8.1). The idempotency policy stores it and returns it again for a repeated key (ADR 0018).
/// </summary>
/// <param name="EntryId">The new entry.</param>
/// <param name="Version">The entry's stream version after Register, which a later command sends back as <c>If-Match</c>.</param>
/// <param name="PartyType">Whether the party is a person or an organisation.</param>
/// <param name="OpenDate">The entry's <c>DirectoryEntryDate</c> of type <c>OpenDate</c>.</param>
/// <param name="DemographicsId">The entry's Demographics Behavior Qualifier.</param>
/// <param name="PartyReferenceId">The entry's Reference Behavior Qualifier.</param>
/// <param name="BankRelationsId">The entry's BankRelations Behavior Qualifier.</param>
public sealed record RegisterPartyAcknowledged(
    PartyReferenceDataDirectoryEntryId EntryId,
    long Version,
    PartyType PartyType,
    DateTimeOffset OpenDate,
    DemographicsId DemographicsId,
    PartyReferenceId PartyReferenceId,
    BankRelationsId BankRelationsId);
