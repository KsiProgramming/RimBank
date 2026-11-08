using RimBank.Postgresql;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.11: the PartyAssociation aggregate in the context's one store, next to the entry streams (ADR 0024). Register
// and Update dispatched through the message bus commit the association's stream, and its inline view follows in the
// same transaction; a stale version is party.version-conflict, a repeated key replays, and the view can be rebuilt.
public sealed class PartyAssociationRoundTripTests(PostgresqlServer server)
{
    private static readonly AssociationValidityPeriod _period = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), null);

    private static readonly AssociationValidityPeriod _ended =
        new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registering_an_association_commits_its_own_stream_and_the_view_follows_at_once()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate) = (await RegisterPartyAsync(host.Bus), await RegisterPartyAsync(host.Bus));

        var registered = (await RegisterAsync(host.Bus, owner, associate, NewKey())).Value;

        registered.ShouldBe(new PartyAssociationAcknowledged(
            registered.AssociationId, 1, owner, associate, PartyRelationshipType.PartyIsParentOfParty, _period));
        (await host.Enquiry.FindAssociationAsync(registered.AssociationId, Cancellation)).ShouldBe(new PartyAssociationView(
            registered.AssociationId.Value, 1, owner, associate, PartyRelationshipType.PartyIsParentOfParty, _period));
        (await host.Enquiry.FindEntryAsync(owner, Cancellation)).ShouldNotBeNull().Version.ShouldBe(1);
        await using var query = host.Store.QuerySession();
        (await query.Events.FetchStreamStateAsync(registered.AssociationId.Value, Cancellation)).ShouldNotBeNull().Version.ShouldBe(1);
    }

    [Fact]
    public async Task Updating_commits_the_next_version_and_the_view_follows_at_once()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate) = (await RegisterPartyAsync(host.Bus), await RegisterPartyAsync(host.Bus));
        var registered = (await RegisterAsync(host.Bus, owner, associate, NewKey())).Value;

        var updated = (await UpdateAsync(host.Bus, owner, registered.AssociationId, 1, NewKey())).Value;

        updated.Version.ShouldBe(2);
        updated.ValidityPeriod.ShouldBe(_ended);
        (await host.Enquiry.FindAssociationAsync(registered.AssociationId, Cancellation)).ShouldBe(new PartyAssociationView(
            registered.AssociationId.Value, 2, owner, associate, PartyRelationshipType.PartyIsParentOfParty, _ended));
    }

    [Fact]
    public async Task An_update_at_a_stale_version_is_a_conflict_and_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate) = (await RegisterPartyAsync(host.Bus), await RegisterPartyAsync(host.Bus));
        var registered = (await RegisterAsync(host.Bus, owner, associate, NewKey())).Value;
        await UpdateAsync(host.Bus, owner, registered.AssociationId, 1, NewKey());

        var stale = await UpdateAsync(host.Bus, owner, registered.AssociationId, 1, NewKey());

        stale.Error.ShouldBe(PartyAssociationErrors.VersionConflict);
        (await host.Enquiry.FindAssociationAsync(registered.AssociationId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task The_same_key_and_payload_replays_the_first_acknowledgement_and_starts_no_second_stream()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate) = (await RegisterPartyAsync(host.Bus), await RegisterPartyAsync(host.Bus));
        var key = NewKey();

        var first = await RegisterAsync(host.Bus, owner, associate, key);
        var again = await RegisterAsync(host.Bus, owner, associate, key);

        again.Value.ShouldBe(first.Value);
        (await host.ScalarAsync<long>(
                "select count(*) from party_reference_data_directory.mt_events where type = 'party_association_registered'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task An_associate_that_is_not_registered_is_refused_and_an_association_is_never_an_entry()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate) = (await RegisterPartyAsync(host.Bus), await RegisterPartyAsync(host.Bus));
        var registered = (await RegisterAsync(host.Bus, owner, associate, NewKey())).Value;

        var unknown = await RegisterAsync(host.Bus, owner, PartyReferenceDataDirectoryEntryId.New(TimeProvider.System), NewKey());
        var association = await RegisterAsync(
            host.Bus, owner, PartyReferenceDataDirectoryEntryId.From(registered.AssociationId.Value), NewKey());

        unknown.Error.ShouldBe(PartyAssociationErrors.AssociateNotRegistered);
        association.Error.ShouldBe(PartyAssociationErrors.AssociateNotRegistered);
        (await host.Enquiry.FindEntryAsync(PartyReferenceDataDirectoryEntryId.From(registered.AssociationId.Value), Cancellation))
            .ShouldBeNull();
    }

    [Fact]
    public async Task Rebuilding_the_view_from_the_event_store_restores_the_association()
    {
        await using var host = await server.StartHostAsync();
        var (owner, associate) = (await RegisterPartyAsync(host.Bus), await RegisterPartyAsync(host.Bus));
        var registered = (await RegisterAsync(host.Bus, owner, associate, NewKey())).Value;
        await UpdateAsync(host.Bus, owner, registered.AssociationId, 1, NewKey());
        var before = await host.Enquiry.FindAssociationAsync(registered.AssociationId, Cancellation);
        await using (var session = host.Store.LightweightSession())
        {
            session.Delete<PartyAssociationView>(registered.AssociationId.Value);
            await session.SaveChangesAsync(Cancellation);
        }

        await host.Store.RebuildViewAsync(
            ViewRegistration.ProjectionName<PartyAssociationViewProjection>(),
            TimeSpan.FromSeconds(60),
            Cancellation);

        (await host.Enquiry.FindAssociationAsync(registered.AssociationId, Cancellation)).ShouldBe(before);
        before.ShouldNotBeNull().Version.ShouldBe(2);
    }

    private static async Task<PartyReferenceDataDirectoryEntryId> RegisterPartyAsync(IMessageBus bus) =>
        (await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(PartyType.Person, NewKey()), Cancellation))
            .Value.EntryId;

    private static Task<Result<PartyAssociationAcknowledged>> RegisterAsync(
        IMessageBus bus,
        PartyReferenceDataDirectoryEntryId owner,
        PartyReferenceDataDirectoryEntryId associate,
        IdempotencyKey key) =>
        bus.InvokeAsync<Result<PartyAssociationAcknowledged>>(
            new RegisterPartyAssociation(owner, new PartyAssociationTerms(associate, PartyRelationshipType.PartyIsParentOfParty, _period), key),
            Cancellation);

    private static Task<Result<PartyAssociationAcknowledged>> UpdateAsync(
        IMessageBus bus,
        PartyReferenceDataDirectoryEntryId owner,
        PartyAssociationId associationId,
        long expectedVersion,
        IdempotencyKey key) =>
        bus.InvokeAsync<Result<PartyAssociationAcknowledged>>(
            new UpdatePartyAssociation(
                owner, associationId, expectedVersion, new PartyAssociationChanges(null, null, new AssociationValidityPeriodChange(_ended)), key),
            Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
