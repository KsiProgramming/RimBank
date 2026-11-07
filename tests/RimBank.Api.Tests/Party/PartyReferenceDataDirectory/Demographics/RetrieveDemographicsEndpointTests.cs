using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.9: BIAN Demographics/Retrieve over HTTP. A query answered from the entry's view with its version as ETag;
// every error is Problem Details (specs/tech-stack.md §5, ADR 0024).
public sealed class RetrieveDemographicsEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Retrieve_shows_what_Update_and_Exchange_recorded_with_the_entry_version()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UrlOf(registered, "Update"), Details, "\"1\"", "\"2\"");
        var exchanged = await PutOkAsync(host, UrlOf(registered, "Exchange"), """{ "ExchangeActionType": "Verify" }""", "\"2\"", "\"3\"");

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, UrlOf(registered, "Retrieve"));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"3\"");
        });

        (await result.ReadAsJsonAsync<DemographicsResponse>()).ShouldBe(exchanged);
        (await result.ReadAsTextAsync()).ShouldNotContain("EthnicityReligion");
    }

    [Fact]
    public async Task Demographics_never_updated_are_unverified_with_no_details()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, UrlOf(registered, "Retrieve"));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });

        (await result.ReadAsJsonAsync<DemographicsResponse>()).ShouldBe(new DemographicsResponse(
            registered.PartyReferenceDataDirectoryEntryId, registered.DemographicsId, null, null, null, null, null, "Unverified", null));
    }

    [Fact]
    public async Task A_current_If_None_Match_is_not_modified()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        await host.Alba.Scenario(_ =>
        {
            Get(_, UrlOf(registered, "Retrieve"));
            _.WithRequestHeader(HeaderNames.IfNoneMatch, "\"1\"");
            _.StatusCodeShouldBe(304);
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });
    }

    [Fact]
    public async Task An_unknown_entry_is_a_party_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await GetProblemAsync(host, Url(Guid.CreateVersion7(), Guid.CreateVersion7(), "Retrieve"), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task Another_entry_s_demographics_id_is_a_behavior_qualifier_not_found_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var problem = await GetProblemAsync(host, Url(registered.PartyReferenceDataDirectoryEntryId, other.DemographicsId, "Retrieve"), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
    }

    [Fact]
    public async Task A_demographics_id_that_RimBank_never_issues_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await GetProblemAsync(host, Url(Guid.CreateVersion7().ToString(), "not-a-guid", "Retrieve"), 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").GetProperty("demographicsid").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Get.Url(Url(Guid.CreateVersion7(), Guid.CreateVersion7(), "Retrieve"));
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }

    private static string UrlOf(RegisterPartyResponse registered, string operation) =>
        DemographicsScenarios.Url(registered.PartyReferenceDataDirectoryEntryId, registered.DemographicsId, operation);

    private static void Get(Scenario scenario, string url)
    {
        scenario.Get.Url(url);
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
    }

    private static async Task<JsonElement> GetProblemAsync(PartyApiHost host, string url, int status)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, url);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(ProblemJson);
        });

        return await ProblemOf(result, status);
    }
}
