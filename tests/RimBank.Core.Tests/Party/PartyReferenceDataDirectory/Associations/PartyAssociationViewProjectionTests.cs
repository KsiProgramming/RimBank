using static RimBank.Party.PartyReferenceDataDirectory.Tests.AssociationExamples;
using AssociationViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationViewProjection,
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationView,
    RimBank.Party.PartyReferenceDataDirectory.PartyAssociationEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The association's view, projected from its events (roadmap 1.11, tech-stack §8.2): Given events → Then view.
public sealed class PartyAssociationViewProjectionTests
{
    [Fact]
    public void A_registered_association_has_a_view_at_version_one() =>
        AssociationViewSpecification.Given(Registered())
            .Then(new PartyAssociationView(AssociationId.Value, 1, Owner, Associate, PartyRelationshipType.PartyIsParentOfParty, Period));

    [Fact]
    public void An_update_replaces_the_type_and_the_period() =>
        AssociationViewSpecification.Given(Registered(), Updated(PartyRelationshipType.PartyIsTrustedByParty, EndedPeriod))
            .Then(new PartyAssociationView(AssociationId.Value, 2, Owner, Associate, PartyRelationshipType.PartyIsTrustedByParty, EndedPeriod));

    [Fact]
    public void An_update_can_clear_the_period() =>
        AssociationViewSpecification.Given(Registered(), Updated(PartyRelationshipType.PartyIsTrustedByParty, EndedPeriod), Updated())
            .Then(new PartyAssociationView(AssociationId.Value, 3, Owner, Associate, PartyRelationshipType.PartyIsParentOfParty, null));

    [Fact]
    public void An_update_before_the_registration_leaves_no_view() =>
        AssociationViewSpecification.Given(Updated()).ThenNoView();

    [Fact]
    public void The_view_names_its_association() =>
        PartyAssociationViewProjection.Project(null, Registered())!.AssociationId.ShouldBe(AssociationId);
}
