using RimBank.Postgresql;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.8: UpdatePartyReferenceDataDirectoryEntry dispatched through the message bus. It appends at the caller's
// expected version, the inline view follows in the same transaction, and a lost race answers party.version-conflict
// (ADR 0024, ADR 0027).
public sealed class UpdatePartyRoundTripTests(PostgresqlServer server)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Updating_an_entry_commits_the_next_version_and_the_view_follows_at_once()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus, PartyType.Person);

        var updated = (await UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Organisation, NewKey())).Value;

        updated.ShouldBe(new UpdatePartyReferenceDataDirectoryEntryAcknowledged(
            registered.EntryId,
            2,
            PartyType.Organisation,
            PostgresqlServer.Now,
            PostgresqlServer.Now,
            registered.DemographicsId,
            registered.PartyReferenceId,
            registered.BankRelationsId));
        var view = await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation);
        view.ShouldBe(new PartyReferenceDataDirectoryEntryView(
            registered.EntryId.Value,
            2,
            PartyType.Organisation,
            PostgresqlServer.Now,
            registered.DemographicsId,
            registered.PartyReferenceId,
            registered.BankRelationsId,
            PostgresqlServer.Now));
        await using var query = host.Store.QuerySession();
        (await query.Events.FetchStreamStateAsync(registered.EntryId.Value, Cancellation)).ShouldNotBeNull().Version.ShouldBe(view!.Version);
    }

    [Fact]
    public async Task Rebuilding_the_view_from_the_event_store_restores_the_update()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus, PartyType.Person);
        await UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Organisation, NewKey());
        var before = await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation);
        await using (var session = host.Store.LightweightSession())
        {
            session.Delete<PartyReferenceDataDirectoryEntryView>(registered.EntryId.Value);
            await session.SaveChangesAsync(Cancellation);
        }

        await host.Store.RebuildViewAsync(
            ViewRegistration.ProjectionName<PartyReferenceDataDirectoryEntryViewProjection>(),
            TimeSpan.FromSeconds(60),
            Cancellation);

        (await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation)).ShouldBe(before);
        before.ShouldNotBeNull().RefreshDate.ShouldBe(PostgresqlServer.Now);
    }

    [Fact]
    public async Task An_update_at_a_stale_version_is_a_conflict_and_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus, PartyType.Person);
        await UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Organisation, NewKey());

        var stale = await UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Person, NewKey());

        stale.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        (await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found()
    {
        await using var host = await server.StartHostAsync();

        var result = await UpdateAsync(host.Bus, PartyReferenceDataDirectoryEntryId.New(TimeProvider.System), 1, null, NewKey());

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
    }

    [Fact]
    public async Task Concurrent_updates_at_the_same_version_commit_once_and_the_others_get_a_conflict_not_an_exception()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus, PartyType.Person);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            Task.Run(() => UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Organisation, NewKey()), Cancellation)));

        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Where(result => result.IsFailure).ShouldAllBe(result => result.Error == PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        await using var query = host.Store.QuerySession();
        (await query.Events.FetchStreamStateAsync(registered.EntryId.Value, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task The_same_key_and_payload_replays_the_first_acknowledgement_and_another_version_is_an_idempotency_conflict()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus, PartyType.Person);
        var key = NewKey();

        var first = await UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Organisation, key);
        var again = await UpdateAsync(host.Bus, registered.EntryId, 1, PartyType.Organisation, key);
        var changed = await UpdateAsync(host.Bus, registered.EntryId, 2, PartyType.Organisation, key);

        again.Value.ShouldBe(first.Value);
        changed.Error.ShouldBe(IdempotencyKey.Reused);
        (await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    private static async Task<RegisterPartyAcknowledged> RegisterAsync(IMessageBus bus, PartyType partyType) =>
        (await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(partyType, NewKey()), Cancellation)).Value;

    private static Task<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>> UpdateAsync(
        IMessageBus bus,
        PartyReferenceDataDirectoryEntryId entryId,
        long expectedVersion,
        PartyType? partyType,
        IdempotencyKey key) =>
        bus.InvokeAsync<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>>(
            new UpdatePartyReferenceDataDirectoryEntry(entryId, expectedVersion, partyType, key), Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
