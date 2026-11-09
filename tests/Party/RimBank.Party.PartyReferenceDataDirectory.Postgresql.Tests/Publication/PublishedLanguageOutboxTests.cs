using System.Text.Json;
using RimBank.SharedKernel;
using Wolverine.Tracking;
using Published = RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.13: an accepted Register or Update publishes exactly one integration event, saved under its v1 identity in
// the context's own schema in the same transaction as the domain events, and a refused, replayed or Behavior Qualifier
// command publishes none (ADR 0017, ADR 0036).
public sealed class PublishedLanguageOutboxTests(PostgresqlServer server)
{
    private const string Schema = "party_reference_data_directory";

    private static readonly JsonSerializerOptions _wire = new(JsonSerializerDefaults.Web);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registering_a_party_publishes_the_entry_created_once_under_its_v1_identity()
    {
        await using var host = await server.StartHostAsync();

        var (session, outcome) = await TrackAsync<RegisterPartyAcknowledged>(host, new RegisterParty(PartyType.Organisation, NewKey()));

        var registered = outcome.Value;
        var created = session.Sent.MessagesOf<Published.PartyReferenceDataDirectoryEntryCreated>().ShouldHaveSingleItem();
        created.ShouldBe(new Published.PartyReferenceDataDirectoryEntryCreated(
            created.EventId,
            PostgresqlServer.Now,
            registered.EntryId.Value,
            1,
            Published.PartyTypes.Organisation,
            Published.DirectoryEntryDateTypes.OpenDate,
            PostgresqlServer.Now));
        session.Sent.AllMessages().OfType<Published.PartyReferenceDataDirectoryEntryUpdated>().ShouldBeEmpty();
        (await StoredAsync<Published.PartyReferenceDataDirectoryEntryCreated>(host, Published.PartyReferenceDataDirectoryEntryCreated.MessageIdentity))
            .ShouldBe([created]);
    }

    [Fact]
    public async Task Updating_an_entry_publishes_the_entry_updated_once_at_its_new_version()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var (session, outcome) = await TrackAsync<UpdatePartyReferenceDataDirectoryEntryAcknowledged>(
            host,
            new UpdatePartyReferenceDataDirectoryEntry(registered.EntryId, 1, PartyType.Organisation, NewKey()));

        outcome.IsSuccess.ShouldBeTrue();
        var updated = session.Sent.MessagesOf<Published.PartyReferenceDataDirectoryEntryUpdated>().ShouldHaveSingleItem();
        updated.ShouldBe(new Published.PartyReferenceDataDirectoryEntryUpdated(
            updated.EventId,
            PostgresqlServer.Now,
            registered.EntryId.Value,
            2,
            Published.PartyTypes.Organisation,
            Published.DirectoryEntryDateTypes.RefreshDate,
            PostgresqlServer.Now));
        (await StoredAsync<Published.PartyReferenceDataDirectoryEntryUpdated>(host, Published.PartyReferenceDataDirectoryEntryUpdated.MessageIdentity))
            .ShouldBe([updated]);
    }

    [Fact]
    public async Task An_update_at_a_stale_version_publishes_nothing()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await host.Bus.InvokeAsync<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>>(
            new UpdatePartyReferenceDataDirectoryEntry(registered.EntryId, 1, PartyType.Organisation, NewKey()),
            Cancellation);

        var (session, outcome) = await TrackAsync<UpdatePartyReferenceDataDirectoryEntryAcknowledged>(
            host,
            new UpdatePartyReferenceDataDirectoryEntry(registered.EntryId, 1, PartyType.Person, NewKey()));

        outcome.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        PublishedIn(session).ShouldBeEmpty();
        (await EnvelopesAsync(host, Published.PartyReferenceDataDirectoryEntryUpdated.MessageIdentity)).ShouldBe(1);
    }

    [Fact]
    public async Task An_update_of_an_unknown_entry_publishes_nothing()
    {
        await using var host = await server.StartHostAsync();

        var (session, outcome) = await TrackAsync<UpdatePartyReferenceDataDirectoryEntryAcknowledged>(
            host,
            new UpdatePartyReferenceDataDirectoryEntry(PartyReferenceDataDirectoryEntryId.New(TimeProvider.System), 1, PartyType.Person, NewKey()));

        outcome.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
        PublishedIn(session).ShouldBeEmpty();
        (await EnvelopesAsync(host, Published.PartyReferenceDataDirectoryEntryUpdated.MessageIdentity)).ShouldBe(0);
    }

    [Fact]
    public async Task A_replayed_registration_publishes_nothing_again()
    {
        await using var host = await server.StartHostAsync();
        var key = NewKey();
        var first = await TrackAsync<RegisterPartyAcknowledged>(host, new RegisterParty(PartyType.Person, key));

        var (replay, outcome) = await TrackAsync<RegisterPartyAcknowledged>(host, new RegisterParty(PartyType.Person, key));

        outcome.Value.ShouldBe(first.Outcome.Value);
        PublishedIn(replay).ShouldBeEmpty();
        (await EnvelopesAsync(host, Published.PartyReferenceDataDirectoryEntryCreated.MessageIdentity)).ShouldBe(1);
    }

    [Fact]
    public async Task A_behavior_qualifier_update_publishes_nothing()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var (session, outcome) = await TrackAsync<UpdateDemographicsAcknowledged>(
            host,
            new UpdateDemographics(
                registered.EntryId,
                registered.DemographicsId,
                1,
                new DemographicsChanges("B2", null, null, null, null),
                NewKey()));

        outcome.IsSuccess.ShouldBeTrue();
        PublishedIn(session).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_committed_control_record_event_has_exactly_one_published_envelope()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await host.Bus.InvokeAsync<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>>(
            new UpdatePartyReferenceDataDirectoryEntry(registered.EntryId, 1, PartyType.Organisation, NewKey()),
            Cancellation);
        await host.Bus.InvokeAsync<Result<UpdatePartyReferenceDataDirectoryEntryAcknowledged>>(
            new UpdatePartyReferenceDataDirectoryEntry(registered.EntryId, 1, PartyType.Person, NewKey()),
            Cancellation);

        var registrations = await host.ScalarAsync<long>($"SELECT count(*) FROM {Schema}.mt_events WHERE type = 'party_registered'");
        var updates = await host.ScalarAsync<long>($"SELECT count(*) FROM {Schema}.mt_events WHERE type = 'party_reference_data_directory_entry_updated'");

        (registrations, updates).ShouldBe((1L, 1L));
        (await EnvelopesAsync(host, Published.PartyReferenceDataDirectoryEntryCreated.MessageIdentity)).ShouldBe(registrations);
        (await EnvelopesAsync(host, Published.PartyReferenceDataDirectoryEntryUpdated.MessageIdentity)).ShouldBe(updates);
        (await host.ScalarAsync<long>("SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE message_type LIKE 'party-reference-data-directory-entry.%'"))
            .ShouldBe(0);
    }

    private static async Task<(ITrackedSession Session, Result<T> Outcome)> TrackAsync<T>(PartyHost host, object command)
    {
        var (session, outcome) = await host.Application.InvokeMessageAndWaitAsync<Result<T>>(command, 30_000);
        return (session, outcome.ShouldNotBeNull());
    }

    private static object[] PublishedIn(ITrackedSession session) =>
        [.. session.Sent.AllMessages().Where(message => message.GetType().Namespace == typeof(Published.PartyTypes).Namespace)];

    private static async Task<RegisterPartyAcknowledged> RegisterAsync(PartyHost host) =>
        (await host.Bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(PartyType.Person, NewKey()), Cancellation)).Value;

    private static Task<long> EnvelopesAsync(PartyHost host, string identity) =>
        host.ScalarAsync<long>($"SELECT count(*) FROM {Schema}.wolverine_incoming_envelopes WHERE message_type = '{identity}'");

    // The stored envelope ends with the message as it goes on the wire: Wolverine's headers, then the JSON payload.
    private static async Task<T?[]> StoredAsync<T>(PartyHost host, string identity)
    {
        var body = await host.ScalarAsync<byte[]>($"SELECT body FROM {Schema}.wolverine_incoming_envelopes WHERE message_type = '{identity}'");
        var payload = body.AsSpan(body.AsSpan().IndexOf("{\""u8));
        return [JsonSerializer.Deserialize<T>(payload, _wire)];
    }

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
