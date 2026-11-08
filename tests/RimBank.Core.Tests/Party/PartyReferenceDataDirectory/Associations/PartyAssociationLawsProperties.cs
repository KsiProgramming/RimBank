using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using AssociationLaws = RimBank.SharedKernel.Testing.DeciderLaws<
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociation,
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The decider laws on the association (ADR 0010, roadmap 1.11), and its own: the owner and the associate never change,
// and an Update with no changes keeps the state. Each runs 100 command sequences.
public sealed class PartyAssociationLawsProperties
{
    private static readonly Gen<PartyReferenceDataDirectoryEntryId> _entries =
        Gen.Elements(AssociationExamples.Owner, AssociationExamples.Associate, AssociationExamples.Stranger);

    private static readonly Gen<PartyRelationshipType> _types = Gen.Frequency(
        (6, Gen.Elements(Enum.GetValues<PartyRelationshipType>())),
        (1, Gen.Constant((PartyRelationshipType)99)));

    private static readonly Gen<AssociationValidityPeriod?> _periods =
        Gen.Elements<AssociationValidityPeriod?>(null, AssociationExamples.Period, AssociationExamples.EndedPeriod);

    private static readonly Gen<bool> _mostlyRegistered = Gen.Frequency((4, Gen.Constant(true)), (1, Gen.Constant(false)));

    // Registers repeat (party.association-already-registered), the owner is sometimes the associate
    // (party.self-association) and either side is sometimes not registered.
    private static readonly Gen<PartyAssociationCommand> _registers =
        from owner in _entries
        from associate in _entries
        from type in _types
        from period in _periods
        from ownerIsRegistered in _mostlyRegistered
        from associateIsRegistered in _mostlyRegistered
        select (PartyAssociationCommand)new RegisterPartyAssociationDetails(
            AssociationExamples.AssociationId,
            owner,
            new PartyAssociationTerms(associate, type, period),
            ownerIsRegistered,
            associateIsRegistered);

    // Updates name any owner and associate, so party.association-not-found and party.associate-cannot-change are common.
    private static readonly Gen<PartyAssociationCommand> _updates =
        from owner in _entries
        from associate in Gen.Frequency(
            (2, Gen.Constant((PartyReferenceDataDirectoryEntryId?)null)),
            (1, _entries.Select(entry => (PartyReferenceDataDirectoryEntryId?)entry)))
        from type in Gen.Frequency(
            (2, Gen.Constant((PartyRelationshipType?)null)),
            (2, _types.Select(type => (PartyRelationshipType?)type)))
        from period in Gen.Frequency(
            (2, Gen.Constant((AssociationValidityPeriodChange?)null)),
            (2, _periods.Select(value => (AssociationValidityPeriodChange?)new AssociationValidityPeriodChange(value))))
        select (PartyAssociationCommand)new UpdatePartyAssociationDetails(owner, new PartyAssociationChanges(associate, type, period));

    private static readonly Gen<PartyAssociationCommand> _commands = Gen.OneOf(_registers, _updates);

    [Property]
    public Property Decisions_are_deterministic() => AssociationLaws.DecisionsAreDeterministic(_commands);

    [Property]
    public Property Decided_events_evolve() => AssociationLaws.DecidedEventsEvolve(_commands);

    [Property]
    public Property The_owner_and_the_associate_never_change_once_registered() =>
        Prop.ForAll(_commands.ListOf().ToArbitrary(), commands =>
        {
            var state = PartyAssociation.Initial;
            (PartyReferenceDataDirectoryEntryId?, PartyReferenceDataDirectoryEntryId?)? parties = null;
            foreach (var command in commands)
            {
                var decided = PartyAssociation.Decide(command, state);
                if (decided.IsSuccess)
                {
                    state = decided.Value.Aggregate(state, PartyAssociation.Evolve);
                }

                parties ??= state.IsRegistered ? (state.OwnerId, state.AssociateId) : null;
                if (parties is { } registered)
                {
                    (state.OwnerId, state.AssociateId).ShouldBe(registered);
                }
            }
        });

    [Property]
    public Property An_update_without_changes_keeps_the_state() =>
        Prop.ForAll(_registers.ToArbitrary(), register =>
        {
            var registered = PartyAssociation.Decide(register, PartyAssociation.Initial);
            if (registered.IsFailure)
            {
                return;
            }

            var state = registered.Value.Aggregate(PartyAssociation.Initial, PartyAssociation.Evolve);
            var update = new UpdatePartyAssociationDetails(state.OwnerId!.Value, PartyAssociationChanges.None);

            PartyAssociation.Decide(update, state).Value.Aggregate(state, PartyAssociation.Evolve).ShouldBe(state);
        });
}
