namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: expectedVersion is MCP's If-Match (ADR 0027, ADR 0040). The version a Retrieve gives lets one Update
// through, that Update's version lets the next one through, and a version already used is a conflict.
public sealed class VersionRoundTripTests(PostgresqlServer server)
{
    [Fact]
    public async Task A_retrieved_version_updates_once_and_the_answers_version_updates_next()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var retrieved = (await PartyReferenceToolTests.RetrieveAsync(host, entry.Id.ToString(), entry.ReferenceId.ToString())).Success();
        var version = retrieved.GetProperty("version").GetInt64();

        var first = (await PartyReferenceToolTests.UpdateAsync(host, entry, """{ "PartyFullLegalName": "Jeanne Dupont" }""", version, PartyTools.Key("first")))
            .Success();
        var stale = await PartyReferenceToolTests.UpdateAsync(host, entry, """{ "PartyNameSalutation": "Ms" }""", version, PartyTools.Key("stale"));
        var next = (await DemographicsToolTests.UpdateAsync(
                host, entry, """{ "EducationHistory": "MSc" }""", first.GetProperty("version").GetInt64(), PartyTools.Key("next")))
            .Success();

        first.GetProperty("version").GetInt64().ShouldBe(version + 1);
        stale.Problem(409, PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code);
        next.GetProperty("version").GetInt64().ShouldBe(version + 2);
        (await host.ScalarAsync<long>(PartyTools.EventsOf(entry.Id))).ShouldBe(3);
    }

    [Fact]
    public async Task The_same_key_and_request_return_the_first_answer_without_a_second_event()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        const string request = """{ "PartyFullLegalName": "Jeanne Dupont" }""";

        var first = (await PartyReferenceToolTests.UpdateAsync(host, entry, request, entry.Version, "reference-once")).Success();
        var again = (await PartyReferenceToolTests.UpdateAsync(host, entry, request, entry.Version, "reference-once")).Success();

        again.GetRawText().ShouldBe(first.GetRawText());
        (await host.ScalarAsync<long>(PartyTools.EventsOf(entry.Id))).ShouldBe(2);
    }

    [Fact]
    public async Task A_missing_expected_version_is_a_tool_arguments_problem_and_nothing_is_stored()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        var result = await host.CallAsync(
            UpdateBankRelationsTool.Name,
            new()
            {
                ["partyReferenceDataDirectoryEntryId"] = entry.Id.ToString(),
                ["bankRelationsId"] = entry.BankRelationsId.ToString(),
                ["request"] = System.Text.Json.JsonElement.Parse("{}"),
                ["idempotencyKey"] = PartyTools.Key("missing"),
            });

        result.Problem(400, "shared.tool-arguments-invalid");
        (await host.ScalarAsync<long>(PartyTools.EventsOf(entry.Id))).ShouldBe(1);
    }
}
