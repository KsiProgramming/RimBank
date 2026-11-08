using static RimBank.Party.PartyReferenceDataDirectory.Tests.AssociationExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Associations/Retrieve flow on the query side (roadmap 1.11, ADR 0024): the view, only for the entry that owns it.
public sealed class RetrievePartyAssociationHandlerTests
{
    private readonly InMemoryPartyDirectoryEnquiry _enquiry = new();

    [Fact]
    public async Task The_owner_retrieves_the_association_view()
    {
        _enquiry.Seed(AssociationId, Registered(), Updated(PartyRelationshipType.PartyIsMarriedToParty, EndedPeriod));

        var view = (await Retrieve(Owner)).Value;

        view.ShouldBe(new PartyAssociationView(
            AssociationId.Value, 2, Owner, Associate, PartyRelationshipType.PartyIsMarriedToParty, EndedPeriod));
    }

    [Fact]
    public async Task An_unknown_association_is_not_found() =>
        (await Retrieve(Owner)).Error.ShouldBe(PartyAssociationErrors.AssociationNotFound);

    [Fact]
    public async Task Another_entry_than_the_owner_does_not_find_it()
    {
        _enquiry.Seed(AssociationId, Registered());

        (await Retrieve(Associate)).Error.ShouldBe(PartyAssociationErrors.AssociationNotFound);
    }

    private Task<RimBank.SharedKernel.Result<PartyAssociationView>> Retrieve(PartyReferenceDataDirectoryEntryId owner) =>
        RetrievePartyAssociationHandler.HandleAsync(
            new RetrievePartyAssociation(owner, AssociationId),
            _enquiry,
            TestContext.Current.CancellationToken);
}
