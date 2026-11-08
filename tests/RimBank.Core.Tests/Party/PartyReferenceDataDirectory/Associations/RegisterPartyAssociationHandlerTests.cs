using RimBank.SharedKernel;
using RimBank.SharedKernel.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.AssociationExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Associations/Register flow on the command side (roadmap 1.11, ADR 0024): the owner and the associate are loaded
// from their entry streams through the write port, the decision runs, and the association's stream starts.
public sealed class RegisterPartyAssociationHandlerTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.From("register-association-1");

    private readonly InMemoryPartyDirectory _directory = new();

    private readonly FixedTimeProvider _time = new(RegisteredAt);

    [Fact]
    public async Task Registering_starts_the_association_stream_and_acknowledges_version_one()
    {
        SeedEntries(Owner, Associate);

        var acknowledged = (await Register(Terms())).Value;

        acknowledged.Version.ShouldBe(1);
        acknowledged.OwnerId.ShouldBe(Owner);
        acknowledged.AssociateId.ShouldBe(Associate);
        acknowledged.AssociateType.ShouldBe(PartyRelationshipType.PartyIsParentOfParty);
        acknowledged.ValidityPeriod.ShouldBe(Period);
        _directory.Associations.ShouldBe([acknowledged.AssociationId]);
        _directory.StreamOf(acknowledged.AssociationId).ShouldBe(
        [
            new PartyAssociationRegistered(acknowledged.AssociationId, Owner, Associate, PartyRelationshipType.PartyIsParentOfParty, Period),
        ]);
    }

    [Fact]
    public async Task The_association_id_is_a_new_version_7_id_from_the_clock()
    {
        SeedEntries(Owner, Associate);

        var acknowledged = (await Register(Terms())).Value;

        acknowledged.AssociationId.Value.Version.ShouldBe(7);
    }

    [Fact]
    public async Task An_unknown_owner_is_not_found_and_nothing_is_started()
    {
        SeedEntries(Associate);

        var result = await Register(Terms());

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
        _directory.Associations.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_owner_cannot_be_its_own_associate()
    {
        SeedEntries(Owner);

        var result = await Register(Terms(associate: Owner));

        result.Error.ShouldBe(PartyAssociationErrors.SelfAssociation);
        _directory.Associations.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_associate_that_is_not_in_the_directory_is_refused()
    {
        SeedEntries(Owner);

        var result = await Register(Terms(associate: Stranger));

        result.Error.ShouldBe(PartyAssociationErrors.AssociateNotRegistered);
        _directory.Associations.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_association_id_is_not_an_entry_and_cannot_be_an_associate()
    {
        SeedEntries(Owner);
        _directory.Seed(AssociationId, Registered());

        var result = await Register(Terms(associate: PartyReferenceDataDirectoryEntryId.From(AssociationId.Value)));

        result.Error.ShouldBe(PartyAssociationErrors.AssociateNotRegistered);
    }

    private void SeedEntries(params PartyReferenceDataDirectoryEntryId[] entries)
    {
        foreach (var entry in entries)
        {
            _directory.Seed(entry, EntryExamples.Registered() with { EntryId = entry });
        }
    }

    private Task<Result<PartyAssociationAcknowledged>> Register(PartyAssociationTerms terms) =>
        RegisterPartyAssociationHandler.HandleAsync(
            new RegisterPartyAssociation(Owner, terms, _key),
            _directory,
            _time,
            TestContext.Current.CancellationToken);
}
