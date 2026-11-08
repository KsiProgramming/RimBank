using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// An input to the association's <see cref="PartyAssociation.Decide"/>. It is not an <see cref="ICommand"/>: the
/// context's command handlers build it from their own commands, with the ids and the write-side checks already set.
/// </summary>
public abstract record PartyAssociationCommand;

/// <summary>
/// Associate the owner with another party (BIAN <c>Associations/Register</c>). The handler creates the association id,
/// and loads the owner's and the associate's entry streams through the write port to tell whether each is registered
/// (ADR 0024, cross-aggregate rule).
/// </summary>
/// <param name="AssociationId">The new association.</param>
/// <param name="OwnerId">The entry that owns the association.</param>
/// <param name="Terms">The associate, type and validity period.</param>
/// <param name="OwnerIsRegistered">Whether the owner's entry stream holds a registered entry.</param>
/// <param name="AssociateIsRegistered">Whether the associate's entry stream holds a registered entry.</param>
public sealed record RegisterPartyAssociationDetails(
    PartyAssociationId AssociationId,
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyAssociationTerms Terms,
    bool OwnerIsRegistered,
    bool AssociateIsRegistered) : PartyAssociationCommand;

/// <summary>
/// Update an association (BIAN <c>Associations/Update</c>): a partial update. The handler has already checked the
/// expected version.
/// </summary>
/// <param name="OwnerId">The entry the caller named: it must own the association.</param>
/// <param name="Changes">The changes to apply.</param>
public sealed record UpdatePartyAssociationDetails(
    PartyReferenceDataDirectoryEntryId OwnerId,
    PartyAssociationChanges Changes) : PartyAssociationCommand;

public sealed partial record PartyAssociation
{
    /// <inheritdoc />
    /// <remarks>
    /// Register is accepted once per association (<c>party.association-already-registered</c>), with a defined type
    /// (<c>party.association-invalid</c>), then needs, in order, a registered owner (<c>party.party-not-found</c>), an
    /// associate other than the owner (<c>party.self-association</c>) and a registered associate
    /// (<c>party.associate-not-registered</c>). Update needs a registered association owned by the caller's entry
    /// (<c>party.association-not-found</c>), a defined type (<c>party.association-invalid</c>) and the same associate
    /// (<c>party.associate-cannot-change</c>), and always records the resulting type and period, even when nothing
    /// changes.
    /// </remarks>
    public static Result<IReadOnlyList<PartyAssociationEvent>> Decide(PartyAssociationCommand command, PartyAssociation state) =>
        (command, state) switch
        {
            (RegisterPartyAssociationDetails, { IsRegistered: true }) =>
                PartyAssociationErrors.AssociationAlreadyRegistered,
            (RegisterPartyAssociationDetails register, _) when !Enum.IsDefined(register.Terms.AssociateType) =>
                PartyAssociationErrors.AssociateTypeInvalid,
            (RegisterPartyAssociationDetails { OwnerIsRegistered: false }, _) =>
                PartyReferenceDataDirectoryEntryErrors.PartyNotFound,
            (RegisterPartyAssociationDetails register, _) when register.Terms.Associate == register.OwnerId =>
                PartyAssociationErrors.SelfAssociation,
            (RegisterPartyAssociationDetails { AssociateIsRegistered: false }, _) =>
                PartyAssociationErrors.AssociateNotRegistered,
            (RegisterPartyAssociationDetails register, _) =>
                Decision.Accept<PartyAssociationEvent>(new PartyAssociationRegistered(
                    register.AssociationId,
                    register.OwnerId,
                    register.Terms.Associate,
                    register.Terms.AssociateType,
                    register.Terms.ValidityPeriod)),
            (UpdatePartyAssociationDetails, { IsRegistered: false }) =>
                PartyAssociationErrors.AssociationNotFound,
            (UpdatePartyAssociationDetails update, _) when update.OwnerId != state.OwnerId =>
                PartyAssociationErrors.AssociationNotFound,
            (UpdatePartyAssociationDetails { Changes.AssociateType: { } type }, _) when !Enum.IsDefined(type) =>
                PartyAssociationErrors.AssociateTypeInvalid,
            (UpdatePartyAssociationDetails { Changes.Associate: { } associate }, _) when associate != state.AssociateId =>
                PartyAssociationErrors.AssociateCannotChange,
            (UpdatePartyAssociationDetails update, { Id: { } id, AssociateType: { } current }) =>
                Decision.Accept<PartyAssociationEvent>(new PartyAssociationUpdated(
                    id,
                    update.Changes.AssociateType ?? current,
                    update.Changes.ValidityPeriod is { } period ? period.Value : state.ValidityPeriod)),
            _ => throw new UnreachableException($"The association does not decide {command}."),
        };
}
