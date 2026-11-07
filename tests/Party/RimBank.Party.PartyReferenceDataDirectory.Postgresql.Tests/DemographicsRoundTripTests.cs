using RimBank.Postgresql;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.9: the Demographics Update, Exchange and Retrieve dispatched through the message bus. Both commands append
// to the entry's stream at the caller's expected version, and the inline view's Demographics section follows in the
// same transaction (ADR 0024, ADR 0027, ADR 0028).
public sealed class DemographicsRoundTripTests(PostgresqlServer server)
{
    private const string Schema = "party_reference_data_directory";

    private static readonly PartyDemographics _details = new(
        "B2",
        new Employment("Contoso Ltd", "Accountant"),
        "Contoso Ltd since 2020",
        "MSc Finance",
        "Large print");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Updating_demographics_commits_the_next_version_and_retrieve_sees_it_at_once()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);

        var updated = (await UpdateAsync(host.Bus, registered, 1, NewKey())).Value;
        var retrieved = (await RetrieveAsync(host.Bus, registered.EntryId, registered.DemographicsId)).Value;

        var expected = new DemographicsRecord(_details, DemographicsVerificationStatus.Unverified, null);
        updated.ShouldBe(new UpdateDemographicsAcknowledged(registered.EntryId, registered.DemographicsId, 2, expected));
        retrieved.Version.ShouldBe(2);
        retrieved.Demographics.ShouldBe(expected);
        await using var query = host.Store.QuerySession();
        (await query.Events.FetchStreamStateAsync(registered.EntryId.Value, Cancellation)).ShouldNotBeNull().Version.ShouldBe(retrieved.Version);
    }

    [Fact]
    public async Task Verifying_demographics_commits_the_verification_and_retrieve_sees_it_at_once()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());

        var exchanged = (await ExchangeAsync(host.Bus, registered, 2, ExchangeActionType.Verify, NewKey())).Value;
        var retrieved = (await RetrieveAsync(host.Bus, registered.EntryId, registered.DemographicsId)).Value;

        var expected = new DemographicsRecord(_details, DemographicsVerificationStatus.Verified, PostgresqlServer.Now);
        exchanged.ShouldBe(new ExchangeDemographicsAcknowledged(registered.EntryId, registered.DemographicsId, 3, expected));
        retrieved.Version.ShouldBe(3);
        retrieved.Demographics.ShouldBe(expected);
    }

    [Fact]
    public async Task The_events_are_stored_under_stable_names_without_ethnicity_or_religion()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());
        await ExchangeAsync(host.Bus, registered, 2, ExchangeActionType.Reject, NewKey());

        (await host.ScalarAsync<string>($"SELECT type FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}' AND version = 2"))
            .ShouldBe("demographics_updated");
        (await host.ScalarAsync<string>($"SELECT type FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}' AND version = 3"))
            .ShouldBe("demographics_exchanged");
        var stored = await host.ScalarAsync<string>(
            $"SELECT string_agg(data::text, ' ') FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}'");
        stored.ShouldContain("Contoso Ltd");
        stored.ShouldNotContain("Ethnicity", Case.Insensitive);
        stored.ShouldNotContain("Religion", Case.Insensitive);
    }

    [Fact]
    public async Task Rebuilding_the_view_from_the_event_store_restores_the_demographics()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());
        await ExchangeAsync(host.Bus, registered, 2, ExchangeActionType.Verify, NewKey());
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
        before.ShouldNotBeNull().CurrentDemographics.VerificationStatus.ShouldBe(DemographicsVerificationStatus.Verified);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());

        var stale = await ExchangeAsync(host.Bus, registered, 1, ExchangeActionType.Verify, NewKey());

        stale.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        (await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task A_foreign_demographics_id_is_not_found_for_commands_and_queries()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        var foreign = registered with { DemographicsId = DemographicsId.New(TimeProvider.System) };

        var update = await UpdateAsync(host.Bus, foreign, 1, NewKey());
        var retrieve = await RetrieveAsync(host.Bus, registered.EntryId, foreign.DemographicsId);

        update.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
        retrieve.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
    }

    private static async Task<RegisterPartyAcknowledged> RegisterAsync(IMessageBus bus) =>
        (await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(PartyType.Person, NewKey()), Cancellation)).Value;

    private static Task<Result<UpdateDemographicsAcknowledged>> UpdateAsync(
        IMessageBus bus,
        RegisterPartyAcknowledged registered,
        long expectedVersion,
        IdempotencyKey key) =>
        bus.InvokeAsync<Result<UpdateDemographicsAcknowledged>>(
            new UpdateDemographics(
                registered.EntryId,
                registered.DemographicsId,
                expectedVersion,
                new DemographicsChanges(
                    _details.SocioEconomicClassification,
                    _details.Employment,
                    _details.EmploymentHistory,
                    _details.EducationHistory,
                    _details.ServicingConstraints),
                key),
            Cancellation);

    private static Task<Result<ExchangeDemographicsAcknowledged>> ExchangeAsync(
        IMessageBus bus,
        RegisterPartyAcknowledged registered,
        long expectedVersion,
        ExchangeActionType action,
        IdempotencyKey key) =>
        bus.InvokeAsync<Result<ExchangeDemographicsAcknowledged>>(
            new ExchangeDemographics(registered.EntryId, registered.DemographicsId, expectedVersion, action, key),
            Cancellation);

    private static Task<Result<PartyReferenceDataDirectoryEntryView>> RetrieveAsync(
        IMessageBus bus,
        PartyReferenceDataDirectoryEntryId entryId,
        DemographicsId demographicsId) =>
        bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(new RetrieveDemographics(entryId, demographicsId), Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
