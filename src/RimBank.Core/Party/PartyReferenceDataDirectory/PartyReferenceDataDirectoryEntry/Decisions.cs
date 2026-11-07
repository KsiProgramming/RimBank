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

/// <summary>
/// Update the entry's details (BIAN <c>PartyReferenceDataDirectoryEntry/Update</c>). The handler takes
/// <paramref name="RefreshDate"/> from its <see cref="TimeProvider"/> and has already checked the expected version.
/// </summary>
/// <param name="PartyType">The new party type, or <c>null</c> to keep the current one.</param>
/// <param name="RefreshDate">The entry's <c>DirectoryEntryDate</c> of type <c>RefreshDate</c>: the time of the update.</param>
public sealed record UpdatePartyReferenceDataDirectoryEntryDetails(
    PartyType? PartyType,
    DateTimeOffset RefreshDate) : PartyReferenceDataDirectoryEntryCommand;

public sealed partial record PartyReferenceDataDirectoryEntry
{
    /// <inheritdoc />
    /// <remarks>
    /// Register is accepted once per entry: a registered entry refuses it with
    /// <c>party.party-already-registered</c>, and a party type other than <see cref="PartyType.Person"/> or
    /// <see cref="PartyType.Organisation"/> is <c>party.party-type-invalid</c>. Update needs a registered entry
    /// (<c>party.party-not-found</c> otherwise) and always records the refresh, even when the party type is unchanged.
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
            (UpdatePartyReferenceDataDirectoryEntryDetails, { Id: null }) =>
                PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (UpdatePartyReferenceDataDirectoryEntryDetails { PartyType: { } partyType }, _) when !Enum.IsDefined(partyType) =>
                PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid,
            (UpdatePartyReferenceDataDirectoryEntryDetails update, { Id: { } entryId, PartyType: { } current }) =>
                Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(new PartyReferenceDataDirectoryEntryUpdated(
                    entryId,
                    update.PartyType ?? current,
                    update.RefreshDate)),
            _ => throw new UnreachableException($"The entry does not decide {command}."),
        };
}
