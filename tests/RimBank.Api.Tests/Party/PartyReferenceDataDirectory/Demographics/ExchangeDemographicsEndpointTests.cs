using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.9: BIAN Demographics/Exchange over HTTP, RimBank's verification of the recorded details (ADR 0028). A
// command with an Idempotency-Key and If-Match, answered from its acknowledgement; every error is Problem Details.
public sealed class ExchangeDemographicsEndpointTests(PostgresqlServer server)
{
    [Theory]
    [InlineData("Verify", "Verified")]
    [InlineData("Reject", "Rejected")]
    public async Task Exchanging_recorded_details_answers_the_verification(string action, string status)
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");

        var exchanged = await PutOkAsync(host, ExchangeUrl(registered), $$"""{ "ExchangeActionType": "{{action}}" }""", "\"2\"", "\"3\"");

        exchanged.VerificationStatus.ShouldBe(status);
        exchanged.VerificationDate.ShouldNotBeNull();
        exchanged.SocioEconomicClassification.ShouldBe("B2");
    }

    [Fact]
    public async Task Changing_verified_details_makes_them_unverified_again()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");
        await PutOkAsync(host, ExchangeUrl(registered), """{ "ExchangeActionType": "Verify" }""", "\"2\"", "\"3\"");

        var unchanged = await PutOkAsync(host, UpdateUrl(registered), "{}", "\"3\"", "\"4\"");
        var changed = await PutOkAsync(host, UpdateUrl(registered), """{ "Socio-EconomicClassification": "C1" }""", "\"4\"", "\"5\"");

        unchanged.VerificationStatus.ShouldBe("Verified");
        changed.VerificationStatus.ShouldBe("Unverified");
        changed.VerificationDate.ShouldBeNull();
    }

    [Fact]
    public async Task Details_never_recorded_are_an_unprocessable_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, ExchangeUrl(registered), """{ "ExchangeActionType": "Verify" }""", "\"1\"", 422);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.demographics-not-recorded");
    }

    [Theory]
    [InlineData("{}", "party.exchange-action-type-required")]
    [InlineData("""{ "ExchangeActionType": "verify" }""", "party.exchange-action-type-invalid")]
    public async Task A_missing_or_unknown_action_is_a_validation_problem(string body, string code)
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, ExchangeUrl(registered), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe(code);
        problem.GetProperty("errors").GetProperty("ExchangeActionType").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");

        var problem = await PutProblemAsync(host, ExchangeUrl(registered), """{ "ExchangeActionType": "Verify" }""", "\"1\"", 409);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.version-conflict");
    }

    [Fact]
    public async Task A_missing_If_Match_is_a_precondition_required_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, ExchangeUrl(registered), """{ "ExchangeActionType": "Verify" }""", ifMatch: null, 428);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-required");
    }

    [Fact]
    public async Task Another_entry_s_demographics_id_is_a_behavior_qualifier_not_found_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host,
            Url(registered.PartyReferenceDataDirectoryEntryId, other.DemographicsId, "Exchange"),
            """{ "ExchangeActionType": "Verify" }""",
            "\"1\"",
            404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
    }

    [Fact]
    public async Task The_same_key_and_request_replays_the_first_answer()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");
        var key = Guid.CreateVersion7().ToString();

        var first = await host.Alba.Scenario(_ =>
        {
            Put(_, ExchangeUrl(registered), """{ "ExchangeActionType": "Verify" }""", "\"2\"", key);
            _.StatusCodeShouldBeOk();
        });
        var again = await host.Alba.Scenario(_ =>
        {
            Put(_, ExchangeUrl(registered), """{ "ExchangeActionType": "Verify" }""", "\"2\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"3\"");
        });

        (await again.ReadAsTextAsync()).ShouldBe(await first.ReadAsTextAsync());
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("""{ "ExchangeActionType": "Verify" }""").ToUrl(Url(Guid.CreateVersion7(), Guid.CreateVersion7(), "Exchange"));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader("Idempotency-Key", Guid.CreateVersion7().ToString());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }

    private static string UpdateUrl(RegisterPartyResponse registered) =>
        Url(registered.PartyReferenceDataDirectoryEntryId, registered.DemographicsId, "Update");

    private static string ExchangeUrl(RegisterPartyResponse registered) =>
        Url(registered.PartyReferenceDataDirectoryEntryId, registered.DemographicsId, "Exchange");
}
