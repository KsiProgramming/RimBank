
namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.4: the Marten write port of the Party Reference Data Directory. Each entry is a stream whose id is the
// entry's id (ADR 0024), stored in the context's own schema under its own role (ADR 0016).
public sealed class MartenPartyDirectoryTests(PostgresqlServer server)
{
    private const string Schema = "party_reference_data_directory";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unknown_entry_has_no_history()
    {
        await using var host = await server.StartHostAsync();
        await using var session = host.Store.LightweightSession();

        var history = await new MartenPartyDirectory(session).LoadEntryAsync(PartyReferenceDataDirectoryEntryId.New(TimeProvider.System), Cancellation);

        history.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_started_entry_loads_back_with_the_same_events()
    {
        await using var host = await server.StartHostAsync();
        var registered = Registered(PartyType.Organisation);

        await StartAsync(host, registered);

        await using var session = host.Store.LightweightSession();
        var history = await new MartenPartyDirectory(session).LoadEntryAsync(registered.EntryId, Cancellation);
        history.ShouldBe([registered]);
    }

    [Fact]
    public async Task Starting_an_existing_entry_fails_at_commit_and_keeps_the_first_stream()
    {
        await using var host = await server.StartHostAsync();
        var registered = Registered(PartyType.Person);
        await StartAsync(host, registered);

        var collision = await Should.ThrowAsync<Exception>(() => StartAsync(host, registered with { PartyType = PartyType.Organisation }));

        collision.GetType().Name.ShouldBe("ExistingStreamIdCollisionException");

        await using var session = host.Store.LightweightSession();
        (await new MartenPartyDirectory(session).LoadEntryAsync(registered.EntryId, Cancellation)).ShouldBe([registered]);
    }

    [Fact]
    public async Task PartyRegistered_is_stored_under_a_stable_name_with_plain_ids_and_the_party_type_name()
    {
        await using var host = await server.StartHostAsync();
        var registered = Registered(PartyType.Person);

        await StartAsync(host, registered);

        (await host.ScalarAsync<string>($"SELECT type FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}'"))
            .ShouldBe("party_registered");
        var data = await host.ScalarAsync<string>($"SELECT data::text FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}'");
        data.ShouldContain($"\"EntryId\": \"{registered.EntryId.Value}\"");
        data.ShouldContain($"\"DemographicsId\": \"{registered.DemographicsId.Value}\"");
        data.ShouldContain("\"PartyType\": \"Person\"");
    }

    [Fact]
    public async Task The_events_live_in_the_context_schema_owned_by_its_own_role()
    {
        await using var host = await server.StartHostAsync();

        (await host.ScalarAsync<string>($"SELECT tableowner::text FROM pg_tables WHERE schemaname = '{Schema}' AND tablename = 'mt_events'"))
            .ShouldBe("rimbank_party_reference_data_directory");
    }

    private static async Task StartAsync(PartyHost host, PartyRegistered registered)
    {
        await using var session = host.Store.LightweightSession();
        new MartenPartyDirectory(session).StartEntry(registered.EntryId, [registered]);
        await session.SaveChangesAsync(Cancellation);
    }

    private static PartyRegistered Registered(PartyType partyType) => new(
        PartyReferenceDataDirectoryEntryId.New(TimeProvider.System),
        partyType,
        PostgresqlServer.Now,
        DemographicsId.New(TimeProvider.System),
        PartyReferenceId.New(TimeProvider.System),
        BankRelationsId.New(TimeProvider.System));
}
