using RimBank.Postgresql;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.12: the BankRelations Update and Retrieve dispatched through the message bus. The Update checks the employee
// or unit on the write side, appends to the entry's stream at the caller's expected version, and the inline view's
// BankRelations section follows in the same transaction (ADR 0024, ADR 0027, ADR 0035).
public sealed class BankRelationsRoundTripTests(PostgresqlServer server)
{
    private const string Schema = "party_reference_data_directory";

    private static readonly DateTimeOffset _start = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Updating_the_bank_relations_commits_the_next_version_and_retrieve_sees_it_at_once()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);
        var manager = await RegisterAsync(host.Bus, PartyType.Person);
        var relation = Relation(manager.EntryId);

        var updated = (await UpdateAsync(host.Bus, customer, 1, NewKey(), BankRelationsChanges.To(relation))).Value;
        var retrieved = (await RetrieveAsync(host.Bus, customer)).Value;

        updated.ShouldBe(new UpdateBankRelationsAcknowledged(customer.EntryId, customer.BankRelationsId, 2, relation));
        retrieved.Version.ShouldBe(2);
        retrieved.BankRelations.ShouldBe(relation);
        await using var query = host.Store.QuerySession();
        (await query.Events.FetchStreamStateAsync(customer.EntryId.Value, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task The_event_is_stored_under_a_stable_name_with_names_and_a_plain_entry_id()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Organisation);
        var manager = await RegisterAsync(host.Bus, PartyType.Person);
        await UpdateAsync(host.Bus, customer, 1, NewKey(), BankRelationsChanges.To(Relation(manager.EntryId)));

        async Task<string> EventPart(string path) => await host.ScalarAsync<string>(
            $"SELECT data #>> '{{Details,{path}}}' FROM {Schema}.mt_events WHERE stream_id = '{customer.EntryId.Value}' AND version = 2");

        (await host.ScalarAsync<string>($"SELECT type FROM {Schema}.mt_events WHERE stream_id = '{customer.EntryId.Value}' AND version = 2"))
            .ShouldBe("party_bank_relations_updated");
        (await EventPart("Type")).ShouldBe("RelationshipManager");
        (await EventPart("LifecycleStatus")).ShouldBe("Active");
        (await EventPart("BusinessUnitEmployee")).ShouldBe(manager.EntryId.ToString());
    }

    [Fact]
    public async Task An_organisation_as_relationship_manager_is_refused_on_the_write_side_and_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);
        var branch = await RegisterAsync(host.Bus, PartyType.Organisation);

        var refused = await UpdateAsync(host.Bus, customer, 1, NewKey(), BankRelationsChanges.To(Relation(branch.EntryId)));

        refused.Error.Code.ShouldBe("party.referenced-party-type-mismatch");
        (await host.Enquiry.FindEntryAsync(customer.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(1);
    }

    [Fact]
    public async Task An_employee_that_is_not_registered_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);

        var refused = await UpdateAsync(
            host.Bus, customer, 1, NewKey(), BankRelationsChanges.To(Relation(PartyReferenceDataDirectoryEntryId.New(TimeProvider.System))));

        refused.Error.Code.ShouldBe("party.referenced-party-not-registered");
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);
        await UpdateAsync(host.Bus, customer, 1, NewKey(), Started());

        var stale = await UpdateAsync(host.Bus, customer, 1, NewKey(), Started());

        stale.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        (await host.Enquiry.FindEntryAsync(customer.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task The_same_key_and_payload_replays_the_first_acknowledgement_and_appends_once()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);
        var key = NewKey();

        var first = await UpdateAsync(host.Bus, customer, 1, key, Started());
        var again = await UpdateAsync(host.Bus, customer, 1, key, Started());

        again.Value.ShouldBe(first.Value);
        (await host.ScalarAsync<long>(
                $"SELECT count(*) FROM {Schema}.mt_events WHERE stream_id = '{customer.EntryId.Value}' AND type = 'party_bank_relations_updated'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Rebuilding_the_view_from_the_event_store_restores_the_bank_relations()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);
        var manager = await RegisterAsync(host.Bus, PartyType.Person);
        await UpdateAsync(host.Bus, customer, 1, NewKey(), BankRelationsChanges.To(Relation(manager.EntryId)));
        var before = await host.Enquiry.FindEntryAsync(customer.EntryId, Cancellation);
        await using (var session = host.Store.LightweightSession())
        {
            session.Delete<PartyReferenceDataDirectoryEntryView>(customer.EntryId.Value);
            await session.SaveChangesAsync(Cancellation);
        }

        await host.Store.RebuildViewAsync(
            ViewRegistration.ProjectionName<PartyReferenceDataDirectoryEntryViewProjection>(),
            TimeSpan.FromSeconds(60),
            Cancellation);

        (await host.Enquiry.FindEntryAsync(customer.EntryId, Cancellation)).ShouldBe(before);
        before.ShouldNotBeNull().CurrentBankRelations.ShouldBe(Relation(manager.EntryId));
    }

    [Fact]
    public async Task A_foreign_bank_relations_id_is_not_found_for_commands_and_queries()
    {
        await using var host = await server.StartHostAsync();
        var customer = await RegisterAsync(host.Bus, PartyType.Person);
        var foreign = customer with { BankRelationsId = BankRelationsId.New(TimeProvider.System) };

        var update = await UpdateAsync(host.Bus, foreign, 1, NewKey(), Started());
        var retrieve = await RetrieveAsync(host.Bus, foreign);

        update.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
        retrieve.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
    }

    private static BankRelationsDetails Relation(PartyReferenceDataDirectoryEntryId employee) =>
        new(BankRelationType.RelationshipManager, employee, _start, null, BankRelationLifecycleStatus.Active);

    private static BankRelationsChanges Started() => new(StartDate: new(_start));

    private static async Task<RegisterPartyAcknowledged> RegisterAsync(IMessageBus bus, PartyType partyType) =>
        (await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(partyType, NewKey()), Cancellation)).Value;

    private static Task<Result<UpdateBankRelationsAcknowledged>> UpdateAsync(
        IMessageBus bus,
        RegisterPartyAcknowledged registered,
        long expectedVersion,
        IdempotencyKey key,
        BankRelationsChanges changes) =>
        bus.InvokeAsync<Result<UpdateBankRelationsAcknowledged>>(
            new UpdateBankRelations(registered.EntryId, registered.BankRelationsId, expectedVersion, changes, key),
            Cancellation);

    private static Task<Result<PartyReferenceDataDirectoryEntryView>> RetrieveAsync(IMessageBus bus, RegisterPartyAcknowledged registered) =>
        bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrieveBankRelations(registered.EntryId, registered.BankRelationsId),
            Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
