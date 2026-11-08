using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.AssociationExamples;
using AssociationSpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociation,
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The PartyAssociation decisions (BIAN Associations/Register and Associations/Update, roadmap 1.11, ADR 0024, ADR 0033).
// Whether the owner and the associate are registered is the handler's write-side load; the expected version is the
// handler's check.
public sealed class PartyAssociationDecisionTests
{
    [Fact]
    public void Registering_an_association_records_its_owner_associate_type_and_period() =>
        AssociationSpecification.Given()
            .When(Register())
            .Then(Registered());

    [Fact]
    public void A_registration_without_a_period_records_none() =>
        AssociationSpecification.Given()
            .When(Register(Terms() with { ValidityPeriod = null }))
            .Then(new PartyAssociationRegistered(AssociationId, Owner, Associate, PartyRelationshipType.PartyIsParentOfParty, null));

    [Fact]
    public void An_unknown_owner_is_not_found_before_anything_else() =>
        AssociationSpecification.Given()
            .When(Register(Terms(associate: Owner), ownerIsRegistered: false, associateIsRegistered: false))
            .ThenFails(ErrorKind.NotFound, "party.party-not-found");

    [Fact]
    public void A_party_cannot_be_associated_with_itself() =>
        AssociationSpecification.Given()
            .When(Register(Terms(associate: Owner)))
            .ThenFails(ErrorKind.BusinessRule, "party.self-association");

    [Fact]
    public void The_associate_must_be_registered_in_the_directory() =>
        AssociationSpecification.Given()
            .When(Register(associateIsRegistered: false))
            .ThenFails(ErrorKind.BusinessRule, "party.associate-not-registered");

    [Fact]
    public void An_undefined_type_is_refused() =>
        AssociationSpecification.Given()
            .When(Register(Terms(type: (PartyRelationshipType)99)))
            .ThenFails(ErrorKind.Validation, "party.association-invalid");

    [Fact]
    public void An_association_is_registered_once() =>
        AssociationSpecification.Given(Registered())
            .When(Register())
            .ThenFails(ErrorKind.Conflict, "party.association-already-registered");

    [Fact]
    public void Updating_the_type_and_the_period_records_both() =>
        AssociationSpecification.Given(Registered())
            .When(Update(new PartyAssociationChanges(
                null,
                PartyRelationshipType.PartyIsTrustedByParty,
                new AssociationValidityPeriodChange(EndedPeriod))))
            .Then(Updated(PartyRelationshipType.PartyIsTrustedByParty, EndedPeriod));

    [Fact]
    public void An_update_without_changes_keeps_everything_and_is_still_recorded() =>
        AssociationSpecification.Given(Registered())
            .When(Update())
            .Then(Updated(PartyRelationshipType.PartyIsParentOfParty, Period));

    [Fact]
    public void A_later_update_starts_from_what_the_previous_one_left() =>
        AssociationSpecification.Given(Registered(), Updated(PartyRelationshipType.PartyIsMarriedToParty, EndedPeriod))
            .When(Update())
            .Then(Updated(PartyRelationshipType.PartyIsMarriedToParty, EndedPeriod));

    [Fact]
    public void An_empty_period_change_clears_the_period() =>
        AssociationSpecification.Given(Registered())
            .When(Update(new PartyAssociationChanges(null, null, new AssociationValidityPeriodChange(null))))
            .Then(Updated(PartyRelationshipType.PartyIsParentOfParty, period: null));

    [Fact]
    public void Naming_the_same_associate_is_accepted() =>
        AssociationSpecification.Given(Registered())
            .When(Update(new PartyAssociationChanges(Associate, null, null)))
            .Then(Updated(PartyRelationshipType.PartyIsParentOfParty, Period));

    [Fact]
    public void The_associate_cannot_change() =>
        AssociationSpecification.Given(Registered())
            .When(Update(new PartyAssociationChanges(Stranger, null, null)))
            .ThenFails(ErrorKind.BusinessRule, "party.associate-cannot-change");

    [Fact]
    public void An_association_that_is_not_registered_is_not_found() =>
        AssociationSpecification.Given()
            .When(Update())
            .ThenFails(ErrorKind.NotFound, "party.association-not-found");

    [Fact]
    public void An_association_of_another_owner_is_not_found() =>
        AssociationSpecification.Given(Registered())
            .When(Update(owner: Stranger))
            .ThenFails(ErrorKind.NotFound, "party.association-not-found");

    [Fact]
    public void An_undefined_type_cannot_be_set() =>
        AssociationSpecification.Given(Registered())
            .When(Update(new PartyAssociationChanges(null, (PartyRelationshipType)99, null)))
            .ThenFails(ErrorKind.Validation, "party.association-invalid");
}
