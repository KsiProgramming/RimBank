using RimBank.Postgresql;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.7: the entry view is projected inline by the core's pure projection, in the context's own store, and the
// Marten read port reads it (ADR 0016, ADR 0024, specs/tech-stack.md §8.2–8.3).
public sealed class MartenPartyDirectoryEnquiryTests(PostgresqlServer server)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unknown_entry_has_no_view()
    {
        await using var host = await server.StartHostAsync();

        var view = await host.Enquiry.FindEntryAsync(PartyReferenceDataDirectoryEntryId.New(TimeProvider.System), Cancellation);

        view.ShouldBeNull();
    }

    [Fact]
    public async Task A_registered_entry_can_be_read_right_after_the_command()
    {
        await using var host = await server.StartHostAsync();

        var acknowledged = await RegisterAsync(host, PartyType.Organisation);

        var view = await host.Enquiry.FindEntryAsync(acknowledged.EntryId, Cancellation);
        view.ShouldBe(new PartyReferenceDataDirectoryEntryView(
            acknowledged.EntryId.Value,
            acknowledged.Version,
            PartyType.Organisation,
            PostgresqlServer.Now,
            acknowledged.DemographicsId,
            acknowledged.PartyReferenceId,
            acknowledged.BankRelationsId));
    }

    [Fact]
    public async Task The_view_version_is_the_entry_stream_version()
    {
        await using var host = await server.StartHostAsync();
        var acknowledged = await RegisterAsync(host, PartyType.Person);

        var view = await host.Enquiry.FindEntryAsync(acknowledged.EntryId, Cancellation);

        await using var query = host.Store.QuerySession();
        var state = await query.Events.FetchStreamStateAsync(acknowledged.EntryId.Value, Cancellation);
        view.ShouldNotBeNull().Version.ShouldBe(state.ShouldNotBeNull().Version);
    }

    [Fact]
    public async Task Rebuilding_the_view_from_the_event_store_restores_it()
    {
        await using var host = await server.StartHostAsync();
        var acknowledged = await RegisterAsync(host, PartyType.Person);
        var before = await host.Enquiry.FindEntryAsync(acknowledged.EntryId, Cancellation);
        await using (var session = host.Store.LightweightSession())
        {
            session.Delete<PartyReferenceDataDirectoryEntryView>(acknowledged.EntryId.Value);
            await session.SaveChangesAsync(Cancellation);
        }

        (await host.Enquiry.FindEntryAsync(acknowledged.EntryId, Cancellation)).ShouldBeNull();
        await host.Store.RebuildViewAsync(
            ViewRegistration.ProjectionName<PartyReferenceDataDirectoryEntryViewProjection>(),
            TimeSpan.FromSeconds(60),
            Cancellation);

        (await host.Enquiry.FindEntryAsync(acknowledged.EntryId, Cancellation)).ShouldBe(before);
    }

    [Fact]
    public async Task The_retrieve_query_through_the_bus_returns_the_view()
    {
        await using var host = await server.StartHostAsync();
        var acknowledged = await RegisterAsync(host, PartyType.Person);

        var retrieved = await host.Bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrievePartyReferenceDataDirectoryEntry(acknowledged.EntryId),
            Cancellation);

        retrieved.Value.EntryId.ShouldBe(acknowledged.EntryId);
        retrieved.Value.PartyType.ShouldBe(PartyType.Person);
    }

    [Fact]
    public async Task The_retrieve_query_for_an_unknown_entry_is_party_not_found()
    {
        await using var host = await server.StartHostAsync();

        var retrieved = await host.Bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrievePartyReferenceDataDirectoryEntry(PartyReferenceDataDirectoryEntryId.New(TimeProvider.System)),
            Cancellation);

        retrieved.IsFailure.ShouldBeTrue();
        retrieved.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
    }

    private static async Task<RegisterPartyAcknowledged> RegisterAsync(PartyHost host, PartyType partyType)
    {
        var result = await host.Bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(
            new RegisterParty(partyType, IdempotencyKey.From(Guid.CreateVersion7().ToString())),
            Cancellation);
        return result.Value;
    }
}
