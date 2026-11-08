using RimBank.Postgresql;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.10 and 1.18: the Reference Update and Retrieve dispatched through the message bus. The Update appends to the entry's
// stream at the caller's expected version, and the inline view's Reference section follows in the same transaction
// (ADR 0024, ADR 0027, ADR 0029).
public sealed class PartyReferenceRoundTripTests(PostgresqlServer server)
{
    private const string Schema = "party_reference_data_directory";

    private static readonly PartyIdentifiers _identifiers = new(
        TaxIdentificationNumber.TryCreate("FR12345678901").Value,
        NationalRegistrationNumber.TryCreate("185057800608436").Value,
        LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value);

    private static readonly PartyReferenceDetails _fullDetails = new(
        _identifiers,
        new PartyNames(PartyFullLegalName.TryCreate("Jeanne Marie Dupont").Value, PartyNameSalutation.TryCreate("Ms").Value),
        new DateOnly(1990, 5, 17),
        new PartyResidency(
            CountryCode.From("FR"),
            CountryCode.From("BE"),
            new ResidencyStatus(ResidencyStatusCode.NonResident, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))));

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
    public async Task Names_date_of_birth_and_residency_round_trip_through_the_stream_and_the_view()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);

        var updated = (await UpdateAsync(host.Bus, registered, 1, NewKey(), ChangesTo(_fullDetails))).Value;
        var retrieved = (await RetrieveAsync(host.Bus, registered.EntryId, registered.PartyReferenceId)).Value;
        var refolded = (await UpdateAsync(host.Bus, registered, 2, NewKey(), PartyReferenceChanges.None)).Value;

        updated.Reference.ShouldBe(_fullDetails);
        retrieved.Reference.ShouldBe(_fullDetails);
        refolded.Reference.ShouldBe(_fullDetails);
    }

    [Fact]
    public async Task Countries_dates_and_the_residency_code_are_stored_as_plain_strings()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey(), ChangesTo(_fullDetails));

        var stored = await host.ScalarAsync<string>(
            $"SELECT data::text FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}' AND version = 2");
        var view = await host.ScalarAsync<string>(
            $"SELECT data::text FROM {Schema}.mt_doc_partyreferencedatadirectoryentryview WHERE id = '{registered.EntryId.Value}'");

        foreach (var json in new[] { stored, view })
        {
            json.ShouldContain("\"FR\"");
            json.ShouldContain("\"BE\"");
            json.ShouldContain("\"1990-05-17\"");
            json.ShouldContain("\"NonResident\"");
            json.ShouldContain("\"Jeanne Marie Dupont\"");
        }
    }

    [Fact]
    public async Task A_reference_event_stored_before_1_18_still_loads_folds_and_rebuilds()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        await UpdateAsync(host.Bus, registered, 1, NewKey());

        // Remove the members 1.18 added, so the event is stored exactly as roadmap 1.10 wrote it.
        await host.ScalarAsync<int>(
            $"UPDATE {Schema}.mt_events SET data = data #- '{{Details,Names}}' #- '{{Details,DateOfBirth}}' #- '{{Details,Residency}}' "
            + $"WHERE stream_id = '{registered.EntryId.Value}' AND version = 2 RETURNING 1");
        (await host.ScalarAsync<string>(
                $"SELECT data::text FROM {Schema}.mt_events WHERE stream_id = '{registered.EntryId.Value}' AND version = 2"))
            .ShouldNotContain("Residency");
        await host.Store.RebuildViewAsync(
            ViewRegistration.ProjectionName<PartyReferenceDataDirectoryEntryViewProjection>(),
            TimeSpan.FromSeconds(60),
            Cancellation);

        var rebuilt = await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation);
        var folded = (await UpdateAsync(host.Bus, registered, 2, NewKey(), PartyReferenceChanges.None)).Value;

        rebuilt.ShouldNotBeNull().CurrentReference.ShouldBe(new PartyReferenceDetails(_identifiers));
        folded.Reference.ShouldBe(new PartyReferenceDetails(_identifiers));
    }

    [Fact]
    public async Task A_date_of_birth_after_today_appends_nothing()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host.Bus);
        var later = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        var refused = await UpdateAsync(
            host.Bus,
            registered,
            1,
            NewKey(),
            PartyReferenceChanges.None with { DateOfBirth = new PartyReferenceChange<DateOnly?>(later) });

        refused.Error.ShouldBe(PartyReferenceErrors.DateOfBirthAfterToday);
        (await host.Enquiry.FindEntryAsync(registered.EntryId, Cancellation)).ShouldNotBeNull().Version.ShouldBe(1);
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
        await UpdateAsync(host.Bus, registered, 1, NewKey(), ChangesTo(_fullDetails));
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
        before.ShouldNotBeNull().CurrentReference.ShouldBe(_fullDetails);
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
        IdempotencyKey key,
        PartyReferenceChanges? changes = null) =>
        bus.InvokeAsync<Result<UpdatePartyReferenceAcknowledged>>(
            new UpdatePartyReference(
                registered.EntryId,
                registered.PartyReferenceId,
                expectedVersion,
                changes ?? ChangesTo(new PartyReferenceDetails(_identifiers)),
                key),
            Cancellation);

    private static PartyReferenceChanges ChangesTo(PartyReferenceDetails details) =>
        new(
            new PartyIdentifierChange<TaxIdentificationNumber>(details.Identifiers.TaxIdentificationNumber),
            new PartyIdentifierChange<NationalRegistrationNumber>(details.Identifiers.NationalRegistrationNumber),
            new PartyIdentifierChange<LegalEntityIdentifier>(details.Identifiers.LegalEntityIdentifier),
            new PartyReferenceChange<PartyFullLegalName?>(details.CurrentNames.FullLegalName),
            new PartyReferenceChange<PartyNameSalutation?>(details.CurrentNames.Salutation),
            new PartyReferenceChange<DateOnly?>(details.DateOfBirth),
            new PartyReferenceChange<CountryCode?>(details.CurrentResidency.Nationality),
            new PartyReferenceChange<CountryCode?>(details.CurrentResidency.CountryOfResidence),
            new PartyReferenceChange<ResidencyStatus?>(details.CurrentResidency.ResidencyStatus));

    private static Task<Result<PartyReferenceDataDirectoryEntryView>> RetrieveAsync(
        IMessageBus bus,
        PartyReferenceDataDirectoryEntryId entryId,
        PartyReferenceId referenceId) =>
        bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(new RetrievePartyReference(entryId, referenceId), Cancellation);

    private static IdempotencyKey NewKey() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
