using RimBank.Postgresql;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.10: the Reference Update and Retrieve dispatched through the message bus. The Update appends to the entry's
// stream at the caller's expected version, and the inline view's Reference section follows in the same transaction
// (ADR 0024, ADR 0027, ADR 0029).
public sealed class PartyReferenceRoundTripTests(PostgresqlServer server)
{
    private const string Schema = "party_reference_data_directory";

    private static readonly PartyIdentifiers _identifiers = new(
        TaxIdentificationNumber.TryCreate("FR12345678901").Value,
        NationalRegistrationNumber.TryCreate("185057800608436").Value,
        LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Updating_the_reference_commits_the_next_version_and_retrieve_sees_it_at_once()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);

        var updated = (await UpdateAsync(host.Bus, registered, 1, NewKey())).Value;
        var retrieved = (await RetrieveAsync(host.Bus, registered.EntryId, registered.PartyReferenceId)).Value;

        var expected = new PartyReferenceDetails(_identifiers);
        updated.ShouldBe(new UpdatePartyReferenceAcknowledged(registered.EntryId, registered.PartyReferenceId, 2, expected));
        retrieved.Version.ShouldBe(2);
        retrieved.Reference.ShouldBe(expected);
        await using var query = host.Store.QuerySession();
        (await query.Events.FetchStreamStateAsync(registered.EntryId.Value, Cancellation)).ShouldNotBeNull().Version.ShouldBe(retrieved.Version);
    }

    [Fact]
    public async Task The_reference_and_the_demographics_are_kept_side_by_side()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await host.Bus.InvokeAsync<Result<UpdateDemographicsAcknowledged>>(
            new UpdateDemographics(registered.EntryId, registered.DemographicsId, 1, DemographicsChanges.None with { EducationHistory = "MSc" }, NewKey()),
            Cancellation);

        await UpdateAsync(host.Bus, registered, 2, NewKey());
        var retrieved = (await RetrieveAsync(host.Bus, registered.EntryId, registered.PartyReferenceId)).Value;

        retrieved.Version.ShouldBe(3);
        retrieved.CurrentDemographics.Details.EducationHistory.ShouldBe("MSc");
        retrieved.CurrentReference.Identifiers.ShouldBe(_identifiers);
    }

    [Fact]
    public async Task The_event_is_stored_under_a_stable_name_with_plain_identifiers()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());

        (await host.ScalarAsync<string>($"SELECT type FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}' AND version = 2"))
            .ShouldBe("party_reference_updated");
        var stored = await host.ScalarAsync<string>(
            $"SELECT data::text FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}' AND version = 2");
        stored.ShouldContain("\"5493001KJTIIGC8Y1R12\"");
    }

    [Fact]
    public async Task Rebuilding_the_view_from_the_event_store_restores_the_reference()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());
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
        before.ShouldNotBeNull().CurrentReference.Identifiers.ShouldBe(_identifiers);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());

        var stale = await UpdateAsync(host.Bus, registered, 1, NewKey());

        stale.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.VersionConflict);
        (await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(2);
    }

    [Fact]
    public async Task A_foreign_reference_id_is_not_found_for_commands_and_queries()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        var foreign = registered with { PartyReferenceId = PartyReferenceId.New(TimeProvider.System) };

        var update = await UpdateAsync(host.Bus, foreign, 1, NewKey());
        var retrieve = await RetrieveAsync(host.Bus, registered.EntryId, foreign.PartyReferenceId);

        update.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
        retrieve.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
    }

    private static async Task<RegisterPartyAcknowledged> RegisterAsync(IMessageBus bus) =>
        (await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(PartyType.Organisation, NewKey()), Cancellation)).Value;

    private static Task<Result<UpdatePartyReferenceAcknowledged>> UpdateAsync(
        IMessageBus bus,
        RegisterPartyAcknowledged registered,
        long expectedVersion,
        IdempotencyKey key) =>
        bus.InvokeAsync<Result<UpdatePartyReferenceAcknowledged>>(
            new UpdatePartyReference(
                registered.EntryId,
                registered.PartyReferenceId,
                expectedVersion,
                new PartyReferenceChanges(
                    new PartyIdentifierChange<TaxIdentificationNumber>(_identifiers.TaxIdentificationNumber),
                    new PartyIdentifierChange<NationalRegistrationNumber>(_identifiers.NationalRegistrationNumber),
                    new PartyIdentifierChange<LegalEntityIdentifier>(_identifiers.LegalEntityIdentifier)),
                key),
            Cancellation);

    private static Task<Result<PartyReferenceDataDirectoryEntryView>> RetrieveAsync(
        IMessageBus bus,
        PartyReferenceDataDirectoryEntryId entryId,
        PartyReferenceId referenceId) =>
        bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(new RetrievePartyReference(entryId, referenceId), Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
