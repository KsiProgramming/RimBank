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

/// <summary>
/// Update the entry's demographic details (BIAN <c>Demographics/Update</c>): a partial update. The handler has already
/// checked the entry, the Demographics id and the expected version.
/// </summary>
/// <param name="DemographicsId">The Demographics the caller named.</param>
/// <param name="Changes">The changes to apply.</param>
public sealed record UpdateDemographicsDetails(
    DemographicsId DemographicsId,
    DemographicsChanges Changes) : PartyReferenceDataDirectoryEntryCommand;

/// <summary>
/// Verify or reject the entry's current demographic details (BIAN <c>Demographics/Exchange</c>, ADR 0028). The handler
/// takes <paramref name="VerificationDate"/> from its <see cref="TimeProvider"/>.
/// </summary>
/// <param name="DemographicsId">The Demographics the caller named.</param>
/// <param name="Action">Whether the bank verifies or rejects the details.</param>
/// <param name="VerificationDate">The time of the verification.</param>
public sealed record ExchangeDemographicsDetails(
    DemographicsId DemographicsId,
    ExchangeActionType Action,
    DateTimeOffset VerificationDate) : PartyReferenceDataDirectoryEntryCommand;

/// <summary>
/// Update the entry's Reference (BIAN <c>Reference/Update</c>): a partial update. The handler has already checked the
/// entry, the Reference id and the expected version, and takes <paramref name="Today"/> from its <see cref="TimeProvider"/>.
/// </summary>
/// <param name="PartyReferenceId">The Reference the caller named.</param>
/// <param name="Changes">The changes to apply.</param>
/// <param name="Today">The UTC date of the update: a date of birth after it is refused.</param>
/// <param name="ReferencedParties">
/// The party type of each registered entry the changes reference (1.21, ADR 0034), loaded by the handler from the write
/// side. A referenced entry missing from it is not registered. <c>null</c> when the changes reference no entry.
/// </param>
public sealed record UpdatePartyReferenceDetails(
    PartyReferenceId PartyReferenceId,
    PartyReferenceChanges Changes,
    DateOnly Today,
    IReadOnlyDictionary<PartyReferenceDataDirectoryEntryId, PartyType>? ReferencedParties = null) : PartyReferenceDataDirectoryEntryCommand;

/// <summary>
/// Update the entry's BankRelations (BIAN <c>BankRelations/Update</c>, roadmap 1.12, ADR 0035): a partial update. The
/// handler has already checked the entry, the BankRelations id and the expected version.
/// </summary>
/// <param name="BankRelationsId">The BankRelations the caller named.</param>
/// <param name="Changes">The changes to apply.</param>
/// <param name="ReferencedPartyType">
/// The party type of the employee or unit to check (<see cref="BankRelationsDetails.ReferenceToCheck"/>), loaded by the
/// handler from the write side, or <c>null</c> when that entry is not registered or there is nothing to check.
/// </param>
public sealed record UpdateBankRelationsDetails(
    BankRelationsId BankRelationsId,
    BankRelationsChanges Changes,
    PartyType? ReferencedPartyType = null) : PartyReferenceDataDirectoryEntryCommand;

public sealed partial record PartyReferenceDataDirectoryEntry
{
    /// <inheritdoc />
    /// <remarks>
    /// Register is accepted once per entry: a registered entry refuses it with
    /// <c>party.party-already-registered</c>, and a party type other than <see cref="PartyType.Person"/> or
    /// <see cref="PartyType.Organisation"/> is <c>party.party-type-invalid</c>. Update needs a registered entry
    /// (<c>party.party-not-found</c> otherwise) and always records the refresh, even when the party type is unchanged; it
    /// refuses to make an organisation with corporate references a person (<c>party.corporate-references-need-organisation</c>).
    /// Demographics Update and Exchange need a registered entry and its own Demographics id
    /// (<c>party.behavior-qualifier-not-found</c> otherwise). Update always records the resulting details, and only a
    /// change makes them unverified again. Exchange needs recorded details (<c>party.demographics-not-recorded</c>).
    /// Reference Update needs a registered entry and its own Reference id, refuses a date of birth or customer-since date
    /// after today, a resulting customer-since date before the date of birth, and a resulting political exposure
    /// description without a foreign or domestic exposure, or a resulting company officer role without an officer
    /// (<c>party.reference-invalid</c>). Corporate references need an organisation's entry, and each reference it sets must
    /// be another registered entry of the right party type (<c>party.corporate-references-need-organisation</c>,
    /// <c>party.self-reference</c>, <c>party.referenced-party-not-registered</c>,
    /// <c>party.referenced-party-type-mismatch</c>). It always records the resulting Reference. BankRelations Update needs a
    /// registered entry and its own BankRelations id, refuses a resulting employee or unit without a type or a relationship
    /// that ends before it starts (<c>party.bank-relations-invalid</c>), and an employee or unit it sets, or keeps under a new
    /// type, that is the entry itself, not registered or of the wrong party type (ADR 0035). It always records the result.
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
            (UpdatePartyReferenceDataDirectoryEntryDetails { PartyType: PartyReferenceDataDirectory.PartyType.Person }, _)
                when state.CurrentReference.CurrentCorporateReferences.HasAny =>
                PartyReferenceErrors.CorporateReferencesNeedOrganisation,
            (UpdatePartyReferenceDataDirectoryEntryDetails update, { Id: { } entryId, PartyType: { } current }) =>
                Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(new PartyReferenceDataDirectoryEntryUpdated(
                    entryId,
                    update.PartyType ?? current,
                    update.RefreshDate)),
            (UpdateDemographicsDetails or ExchangeDemographicsDetails or UpdatePartyReferenceDetails or UpdateBankRelationsDetails, { Id: null }) =>
                PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (UpdateDemographicsDetails update, _) when update.DemographicsId != state.DemographicsId =>
                PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (ExchangeDemographicsDetails exchange, _) when exchange.DemographicsId != state.DemographicsId =>
                PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (UpdateDemographicsDetails update, { Id: { } entryId }) =>
                DecideDemographicsUpdate(entryId, update, state.CurrentDemographics),
            (ExchangeDemographicsDetails exchange, _) when !Enum.IsDefined(exchange.Action) =>
                DemographicsErrors.ExchangeActionTypeInvalid,
            (ExchangeDemographicsDetails, { CurrentDemographics.Details.HasDetails: false }) =>
                DemographicsErrors.DemographicsNotRecorded,
            (ExchangeDemographicsDetails exchange, { Id: { } entryId }) =>
                Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(new DemographicsExchanged(
                    entryId,
                    exchange.DemographicsId,
                    exchange.Action switch
                    {
                        ExchangeActionType.Verify => DemographicsVerificationStatus.Verified,
                        ExchangeActionType.Reject => DemographicsVerificationStatus.Rejected,
                        _ => throw new UnreachableException($"Undefined action {exchange.Action} was refused above."),
                    },
                    exchange.VerificationDate)),
            (UpdatePartyReferenceDetails update, _) when update.PartyReferenceId != state.PartyReferenceId =>
                PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (UpdatePartyReferenceDetails { Changes.DateOfBirth.Value: { } dateOfBirth } update, _) when dateOfBirth > update.Today =>
                PartyReferenceErrors.DateOfBirthAfterToday,
            (UpdatePartyReferenceDetails { Changes.CustomerSinceDate.Value: { } since } update, _) when since > update.Today =>
                PartyReferenceErrors.CustomerSinceDateAfterToday,
            (UpdatePartyReferenceDetails update, { Id: { } entryId }) =>
                DecideReferences(entryId, state.PartyType, update, state.CurrentReference.Apply(update.Changes)),
            (UpdateBankRelationsDetails update, _) when update.BankRelationsId != state.BankRelationsId =>
                PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound,
            (UpdateBankRelationsDetails update, { Id: { } entryId }) =>
                DecideBankRelations(entryId, update, state.CurrentBankRelations),
            _ => throw new UnreachableException($"The entry does not decide {command}."),
        };

    // The references to other parties (roadmap 1.21, ADR 0034): only an organisation's entry holds them, and each one the
    // Update sets names another registered entry of the right party type. The handler loaded those entries from the write
    // side; references the Update keeps were checked when they were recorded.
    private static Result<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> DecideReferences(
        PartyReferenceDataDirectoryEntryId entryId,
        PartyType? partyType,
        UpdatePartyReferenceDetails update,
        PartyReferenceDetails updated)
    {
        if (updated.CurrentCorporateReferences.HasAny && partyType != PartyReferenceDataDirectory.PartyType.Organisation)
        {
            return PartyReferenceErrors.CorporateReferencesNeedOrganisation;
        }

        foreach (var (field, referenced) in update.Changes.ReferencedEntries())
        {
            var expected = field == PartyReferenceFields.CompanyOfficerPartyReference ? PartyReferenceDataDirectory.PartyType.Person : PartyReferenceDataDirectory.PartyType.Organisation;
            PartyType? actualType = update.ReferencedParties?.TryGetValue(referenced, out var loaded) == true ? loaded : null;
            var refused = (referenced == entryId, actualType) switch
            {
                (true, _) => PartyReferenceErrors.SelfReference(field),
                (false, null) => PartyReferenceErrors.ReferencedPartyNotRegistered(field),
                (false, { } actual) when actual != expected => PartyReferenceErrors.ReferencedPartyTypeMismatch(field, expected),
                _ => null,
            };
            if (refused is not null)
            {
                return refused;
            }
        }

        return DecideReferenceUpdate(entryId, update, updated);
    }

    // The rules that hold across Reference fields are checked on the resulting Reference (roadmap 1.20, ADR 0032), so a
    // partial update cannot leave it inconsistent with values it keeps.
    private static Result<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> DecideReferenceUpdate(
        PartyReferenceDataDirectoryEntryId entryId,
        UpdatePartyReferenceDetails update,
        PartyReferenceDetails updated) =>
        (updated.CurrentLegalStanding, updated.DateOfBirth) switch
        {
            ({ PoliticalExposureDescription: not null, PoliticalExposureType: var type }, _) when type?.IsExposed() != true =>
                PartyReferenceErrors.PoliticalExposureDescriptionWithoutExposure,
            ({ CustomerSince: { } since }, { } born) when since < born =>
                PartyReferenceErrors.CustomerSinceDateBeforeDateOfBirth,
            _ when updated.CurrentCorporateReferences is { CompanyOfficerRole: not null, CompanyOfficer: null } =>
                PartyReferenceErrors.CompanyOfficerRoleWithoutOfficer,
            _ => Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(
                new PartyReferenceUpdated(entryId, update.PartyReferenceId, updated)),
        };

    // The BankRelations rules (roadmap 1.12, ADR 0035), checked on the resulting BankRelations so a partial update cannot
    // leave it inconsistent with values it keeps: an employee or unit needs a type, the relationship does not end before it
    // starts, and the employee or unit to check is another registered entry of the party type the relation type needs.
    private static Result<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> DecideBankRelations(
        PartyReferenceDataDirectoryEntryId entryId,
        UpdateBankRelationsDetails update,
        BankRelationsDetails current)
    {
        var updated = current.Apply(update.Changes);
        const string field = BankRelationsFields.BusinessUnitEmployeePartyReference;
        return (updated, current.ReferenceToCheck(update.Changes), update.ReferencedPartyType) switch
        {
            ({ BusinessUnitEmployee: not null, Type: null }, _, _) => BankRelationsErrors.ReferenceWithoutType,
            ({ StartDate: { } start, EndDate: { } end }, _, _) when end < start => BankRelationsErrors.EndDateBeforeStartDate,
            (_, { } referenced, _) when referenced == entryId => PartyReferenceErrors.SelfReference(field),
            (_, not null, null) => PartyReferenceErrors.ReferencedPartyNotRegistered(field),
            ({ Type: { } type }, not null, { } actual) when actual != type.ReferencedPartyType() =>
                PartyReferenceErrors.ReferencedPartyTypeMismatch(field, type.ReferencedPartyType()),
            _ => Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(
                new BankRelationsUpdated(entryId, update.BankRelationsId, updated)),
        };
    }

    private static Result<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> DecideDemographicsUpdate(
        PartyReferenceDataDirectoryEntryId entryId,
        UpdateDemographicsDetails update,
        DemographicsRecord current)
    {
        var updated = current.UpdatedTo(current.Details.Apply(update.Changes));
        return Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(new DemographicsUpdated(
            entryId,
            update.DemographicsId,
            updated.Details,
            updated.VerificationStatus,
            updated.VerificationDate));
    }
}
