using System.Diagnostics;
using RimBank.Party.PartyReferenceDataDirectory.BankRelations;
using RimBank.Party.PartyReferenceDataDirectory.Demographics;
using RimBank.Party.PartyReferenceDataDirectory.Reference;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;

/// <summary>
/// A Party Reference Data Directory entry, the Control Record and aggregate root of this context (ADR 0024): the fold of
/// its event stream. Every member is <c>null</c> until the entry is registered.
/// </summary>
/// <param name="Id">The entry.</param>
/// <param name="PartyType">Whether the party is a person or an organisation.</param>
/// <param name="OpenDate">The entry's <c>DirectoryEntryDate</c> of type <c>OpenDate</c>.</param>
/// <param name="DemographicsId">The entry's Demographics Behavior Qualifier.</param>
/// <param name="PartyReferenceId">The entry's Reference Behavior Qualifier.</param>
/// <param name="BankRelationsId">The entry's BankRelations Behavior Qualifier.</param>
public sealed partial record PartyReferenceDataDirectoryEntry(
    PartyReferenceDataDirectoryEntryId? Id,
    PartyType? PartyType,
    DateTimeOffset? OpenDate,
    DemographicsId? DemographicsId,
    PartyReferenceId? PartyReferenceId,
    BankRelationsId? BankRelationsId)
    : IDecider<PartyReferenceDataDirectoryEntry, PartyReferenceDataDirectoryEntryCommand, PartyReferenceDataDirectoryEntryEvent>
{
    /// <summary>An entry that is not registered yet.</summary>
    public static PartyReferenceDataDirectoryEntry Initial { get; } = new(null, null, null, null, null, null);

    /// <summary>Whether the entry has been registered.</summary>
    public bool IsRegistered => Id is not null;

    /// <inheritdoc />
    public static PartyReferenceDataDirectoryEntry Evolve(
        PartyReferenceDataDirectoryEntry state,
        PartyReferenceDataDirectoryEntryEvent domainEvent) =>
        domainEvent switch
        {
            PartyRegistered registered => new PartyReferenceDataDirectoryEntry(
                registered.EntryId,
                registered.PartyType,
                registered.OpenDate,
                registered.DemographicsId,
                registered.PartyReferenceId,
                registered.BankRelationsId),
            _ => throw new UnreachableException($"The entry does not know {domainEvent}."),
        };
}
