using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.AssociationExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Associations/Update flow on the command side (roadmap 1.11, ADR 0027): the handler checks that the association
// exists and is the caller's entry's, then the expected version, decides, appends with that version and acknowledges
// from the decision.
public sealed class UpdatePartyAssociationHandlerTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.From("update-association-1");

    private static readonly PartyAssociationChanges _end =
        new(null, null, new AssociationValidityPeriodChange(EndedPeriod));

    private readonly InMemoryPartyDirectory _directory = new();

    [Fact]
    public async Task Updating_appends_the_update_and_acknowledges_the_next_version()
    {
        _directory.Seed(AssociationId, Registered());

        var acknowledged = (await Update(expectedVersion: 1, _end)).Value;

        _directory.StreamOf(AssociationId).ShouldBe([Registered(), Updated(PartyRelationshipType.PartyIsParentOfParty, EndedPeriod)]);
        acknowledged.ShouldBe(new PartyAssociationAcknowledged(
            AssociationId, 2, Owner, Associate, PartyRelationshipType.PartyIsParentOfParty, EndedPeriod));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task An_expected_version_that_is_not_the_current_one_is_a_conflict_and_nothing_is_appended(long expectedVersion)
    {
        _directory.Seed(AssociationId, Registered());

        var result = await Update(expectedVersion, _end);

        result.Error.ShouldBe(PartyAssociationErrors.VersionConflict);
        result.Error.Code.ShouldBe("party.version-conflict");
        _directory.StreamOf(AssociationId).ShouldBe([Registered()]);
    }

    [Fact]
    public async Task An_unknown_association_is_not_found_before_the_version_is_checked()
    {
        var result = await Update(expectedVersion: 7, _end);

        result.Error.ShouldBe(PartyAssociationErrors.AssociationNotFound);
    }

    [Fact]
    public async Task An_association_of_another_owner_is_not_found_before_the_version_is_checked()
    {
        _directory.Seed(AssociationId, Registered());

        var result = await Update(expectedVersion: 7, _end, owner: Stranger);

        result.Error.ShouldBe(PartyAssociationErrors.AssociationNotFound);
    }

    [Fact]
    public async Task Another_associate_is_refused_after_the_version_check()
    {
        _directory.Seed(AssociationId, Registered());

        var result = await Update(expectedVersion: 1, new PartyAssociationChanges(Stranger, null, null));

        result.Error.ShouldBe(PartyAssociationErrors.AssociateCannotChange);
        _directory.StreamOf(AssociationId).ShouldBe([Registered()]);
    }

    private Task<Result<PartyAssociationAcknowledged>> Update(
        long expectedVersion,
        PartyAssociationChanges changes,
        PartyReferenceDataDirectoryEntryId? owner = null) =>
        UpdatePartyAssociationHandler.HandleAsync(
            new UpdatePartyAssociation(owner ?? Owner, AssociationId, expectedVersion, changes, _key),
            _directory,
            TestContext.Current.CancellationToken);
}
