using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Registration;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.4: RegisterParty dispatched through the message bus, as the .Api (1.5) and .Mcp (1.16) adapters will send
// it. The handler commits through the context's own store, once per idempotency key (ADR 0017, ADR 0018).
public sealed class RegisterPartyRoundTripTests(PostgresqlServer server)
{
    private const string Streams = "SELECT count(*) FROM party_reference_data_directory.mt_streams";
    private const string Outcomes = "SELECT count(*) FROM party_reference_data_directory.mt_doc_idempotentoutcome";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registering_a_party_commits_its_entry_stream_at_the_acknowledged_version()
    {
        await using var host = await server.StartHostAsync();

        var result = await RegisterAsync(host.Bus, PartyType.Person, NewKey());

        var acknowledged = result.Value;
        acknowledged.Version.ShouldBe(1);
        acknowledged.OpenDate.ShouldBe(PostgresqlServer.Now);
        await using var query = host.Store.QuerySession();
        var stream = await query.Events.FetchStreamAsync(acknowledged.EntryId.Value, token: Cancellation);
        stream.Select(stored => stored.Data).ShouldBe([
            new PartyRegistered(
                acknowledged.EntryId,
                PartyType.Person,
                PostgresqlServer.Now,
                acknowledged.DemographicsId,
                acknowledged.PartyReferenceId,
                acknowledged.BankRelationsId),
        ]);
        var state = await query.Events.FetchStreamStateAsync(acknowledged.EntryId.Value, Cancellation);
        state.ShouldNotBeNull().Version.ShouldBe(acknowledged.Version);
    }

    [Fact]
    public async Task The_same_key_and_payload_replays_the_first_acknowledgement_without_a_second_entry()
    {
        await using var host = await server.StartHostAsync();
        var key = NewKey();

        var first = await RegisterAsync(host.Bus, PartyType.Organisation, key);
        var again = await RegisterAsync(host.Bus, PartyType.Organisation, key);

        again.IsSuccess.ShouldBeTrue();
        again.Value.ShouldBe(first.Value);
        (await host.ScalarAsync<long>(Streams)).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_with_another_party_type_is_an_idempotency_conflict()
    {
        await using var host = await server.StartHostAsync();
        var key = NewKey();
        await RegisterAsync(host.Bus, PartyType.Person, key);

        var changed = await RegisterAsync(host.Bus, PartyType.Organisation, key);

        changed.IsFailure.ShouldBeTrue();
        changed.Error.ShouldBe(IdempotencyKey.Reused);
        (await host.ScalarAsync<long>(Streams)).ShouldBe(1);
    }

    [Fact]
    public async Task An_undefined_party_type_is_refused_and_stores_nothing()
    {
        await using var host = await server.StartHostAsync();

        var refused = await RegisterAsync(host.Bus, (PartyType)99, NewKey());

        refused.IsFailure.ShouldBeTrue();
        refused.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid);
        (await host.ScalarAsync<long>(Streams)).ShouldBe(0);
        (await host.ScalarAsync<long>(Outcomes)).ShouldBe(0);
    }

    private static Task<Result<RegisterPartyAcknowledged>> RegisterAsync(IMessageBus bus, PartyType partyType, IdempotencyKey key) =>
        bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(partyType, key), Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
