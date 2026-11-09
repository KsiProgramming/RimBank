using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the Demographics tools send the same Update, Exchange and Retrieve as the HTTP endpoints, with
// expectedVersion in place of If-Match and the BIAN body as their request argument (ADR 0028, ADR 0039, ADR 0040).
public sealed class DemographicsToolTests(PostgresqlServer server)
{
    private const string Details = """
        {
          "Socio-EconomicClassification": "Professional",
          "Employment": { "PartyReference": "Acme", "InvolvementReference": "Engineer" },
          "EducationHistory": "MSc",
          "EthnicityReligion": "never stored"
        }
        """;

    [Fact]
    public async Task Update_then_retrieve_answers_the_details_and_the_versions()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        var updated = (await UpdateAsync(host, entry, Details, entry.Version, PartyTools.Key("update"))).Success();
        var retrieved = (await RetrieveAsync(host, entry.Id.ToString(), entry.DemographicsId.ToString())).Success();

        updated.GetProperty("Socio-EconomicClassification").GetString().ShouldBe("Professional");
        updated.GetProperty("Employment").GetProperty("InvolvementReference").GetString().ShouldBe("Engineer");
        updated.GetProperty("VerificationStatus").GetString().ShouldBe("Unverified");
        updated.TryGetProperty("EthnicityReligion", out _).ShouldBeFalse();
        updated.GetProperty("version").GetInt64().ShouldBe(entry.Version + 1);
        JsonElement.DeepEquals(retrieved, updated).ShouldBeTrue($"{retrieved} vs {updated}");
    }

    [Fact]
    public async Task A_left_out_field_keeps_its_value_and_an_empty_one_clears_it()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var first = (await UpdateAsync(host, entry, Details, entry.Version, PartyTools.Key("first"))).Success();

        var second = (await UpdateAsync(host, entry, """{ "EducationHistory": "" }""", first.GetProperty("version").GetInt64(), PartyTools.Key("second")))
            .Success();

        second.GetProperty("Socio-EconomicClassification").GetString().ShouldBe("Professional");
        second.TryGetProperty("EducationHistory", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Exchange_verifies_the_recorded_details()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var updated = (await UpdateAsync(host, entry, Details, entry.Version, PartyTools.Key("update"))).Success();

        var verified = (await ExchangeAsync(host, entry, """{ "ExchangeActionType": "Verify" }""", updated.GetProperty("version").GetInt64(), PartyTools.Key("verify")))
            .Success();

        verified.GetProperty("VerificationStatus").GetString().ShouldBe("Verified");
        verified.GetProperty("VerificationDate").GetDateTimeOffset().ShouldBe(PostgresqlServer.Now);
        verified.GetProperty("version").GetInt64().ShouldBe(entry.Version + 2);
    }

    [Theory]
    [InlineData("{}", 400, "party.exchange-action-type-required")]
    [InlineData("""{ "ExchangeActionType": "Approve" }""", 400, "party.exchange-action-type-invalid")]
    [InlineData("""{ "ExchangeActionType": "Verify" }""", 422, "party.demographics-not-recorded")]
    public async Task An_exchange_without_a_valid_action_or_recorded_details_is_refused(string request, int status, string errorCode)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        (await ExchangeAsync(host, entry, request, entry.Version, PartyTools.Key("exchange"))).Problem(status, errorCode);
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        (await UpdateAsync(host, entry, Details, entry.Version, PartyTools.Key("first"))).Success();

        (await UpdateAsync(host, entry, Details, entry.Version, PartyTools.Key("stale")))
            .Problem(409, PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code);
    }

    [Theory]
    [InlineData("not-a-guid", 400, "shared.invalid-identifier")]
    [InlineData("01a1b031-8dc0-74cc-a63a-f954f0f3f1f4", 404, "party.behavior-qualifier-not-found")]
    public async Task A_malformed_or_unknown_demographics_id_is_refused(string demographicsId, int status, string errorCode)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        (await RetrieveAsync(host, entry.Id.ToString(), demographicsId)).Problem(status, errorCode);
    }

    internal static Task<CallToolResult> UpdateAsync(PartyMcpHost host, Entry entry, string request, long expectedVersion, string key) =>
        host.CallAsync(UpdateDemographicsTool.Name, Command(entry, request, expectedVersion, key));

    internal static Task<CallToolResult> ExchangeAsync(PartyMcpHost host, Entry entry, string request, long expectedVersion, string key) =>
        host.CallAsync(ExchangeDemographicsTool.Name, Command(entry, request, expectedVersion, key));

    internal static Task<CallToolResult> RetrieveAsync(PartyMcpHost host, string entryId, string demographicsId) =>
        host.CallAsync(
            RetrieveDemographicsTool.Name,
            new() { ["partyReferenceDataDirectoryEntryId"] = entryId, ["demographicsId"] = demographicsId });

    private static Dictionary<string, object?> Command(Entry entry, string request, long expectedVersion, string key) =>
        new()
        {
            ["partyReferenceDataDirectoryEntryId"] = entry.Id.ToString(),
            ["demographicsId"] = entry.DemographicsId.ToString(),
            ["request"] = JsonElement.Parse(request),
            ["expectedVersion"] = expectedVersion,
            ["idempotencyKey"] = key,
        };
}
