using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// An input to the entry's <see cref="PartyReferenceDataDirectoryEntry.Decide"/>. It is not an <see cref="ICommand"/>:
/// the context's command handlers build it from their own commands, with the ids and the time already set.
/// </summary>
public abstract record PartyReferenceDataDirectoryEntryCommand;

/// <summary>
/// Register a party in the directory (BIAN <c>PartyReferenceDataDirectoryEntry/Register</c>). The handler creates the
/// entry id and the three singleton Behavior Qualifier ids, and takes <paramref name="OpenDate"/> from its
/// <see cref="TimeProvider"/>.
/// </summary>
/// <param name="EntryId">The new entry.</param>
/// <param name="PartyType">Whether the party is a person or an organisation.</param>
/// <param name="OpenDate">The entry's <c>DirectoryEntryDate</c> of type <c>OpenDate</c>.</param>
/// <param name="DemographicsId">The id the entry's Demographics will have.</param>
/// <param name="PartyReferenceId">The id the entry's Reference will have.</param>
/// <param name="BankRelationsId">The id the entry's BankRelations will have.</param>
public sealed record RegisterPartyReferenceDataDirectoryEntry(
    PartyReferenceDataDirectoryEntryId EntryId,
    PartyType PartyType,
    DateTimeOffset OpenDate,
    DemographicsId DemographicsId,
    PartyReferenceId PartyReferenceId,
    BankRelationsId BankRelationsId) : PartyReferenceDataDirectoryEntryCommand;

public sealed partial record PartyReferenceDataDirectoryEntry
{
    /// <inheritdoc />
    /// <remarks>
    /// Register is accepted once per entry: a registered entry refuses it with
    /// <c>party.party-already-registered</c>, and a party type other than <see cref="PartyType.Person"/> or
    /// <see cref="PartyType.Organisation"/> is <c>party.party-type-invalid</c>.
    /// </remarks>
    public static Result<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> Decide(
        PartyReferenceDataDirectoryEntryCommand command,
        PartyReferenceDataDirectoryEntry state) =>
        (command, state) switch
        {
            (RegisterPartyReferenceDataDirectoryEntry register, _) when !Enum.IsDefined(register.PartyType) =>
                PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid,
            (RegisterPartyReferenceDataDirectoryEntry, { IsRegistered: true }) =>
                PartyReferenceDataDirectoryEntryErrors.PartyAlreadyRegistered,
            (RegisterPartyReferenceDataDirectoryEntry register, _) =>
                Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(new PartyRegistered(
                    register.EntryId,
                    register.PartyType,
                    register.OpenDate,
                    register.DemographicsId,
                    register.PartyReferenceId,
                    register.BankRelationsId)),
            _ => throw new UnreachableException($"The entry does not decide {command}."),
        };
}
