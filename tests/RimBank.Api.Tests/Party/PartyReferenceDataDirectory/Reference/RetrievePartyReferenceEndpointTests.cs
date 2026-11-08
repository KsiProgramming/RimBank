using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.ReferenceScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.10 and 1.18: BIAN Reference/Retrieve over HTTP. A query answered from the entry's view with its version as ETag;
// every error is Problem Details (specs/tech-stack.md §5, ADR 0024).
public sealed class RetrievePartyReferenceEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Retrieve_shows_what_Update_recorded_with_the_entry_version()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var updated = await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, RetrieveUrl(registered));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        (await result.ReadAsJsonAsync<PartyReferenceResponse>()).ShouldBe(updated);
    }

    [Fact]
    public async Task Retrieve_shows_the_names_date_of_birth_and_residency()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");
        await PutReferenceOkAsync(host, UpdateUrl(registered), NamesAndResidency, "\"2\"", "\"3\"");

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, RetrieveUrl(registered));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"3\"");
        });

        var body = await result.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("DateofBirth").GetString().ShouldBe("1990-05-17");
        body.GetProperty("Nationality").GetProperty("Code").GetString().ShouldBe("FR");
        body.GetProperty("ResidencyStatus").GetProperty("StatusDescription").GetString().ShouldBe("NonResident");
        body.GetProperty("ResidencyStatus").GetProperty("StatusDateTime").GetString().ShouldBe("2024-01-01T00:00:00+00:00");
        body.Deserialize<PartyReferenceResponse>(JsonSerializerOptions.Web).ShouldBe(ExpectedWithNamesAndResidency(registered));
    }

    [Fact]
    public async Task A_reference_never_updated_answers_the_ids_only()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, RetrieveUrl(registered));
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });

        (await result.ReadAsJsonAsync<JsonElement>()).EnumerateObject().Select(property => property.Name)
            .ShouldBe(["PartyReferenceDataDirectoryEntryId", "ReferenceId"]);
    }

    [Fact]
    public async Task A_current_If_None_Match_is_not_modified()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutReferenceOkAsync(host, UpdateUrl(registered), Identifiers, "\"1\"", "\"2\"");

        await host.Alba.Scenario(_ =>
        {
            Get(_, RetrieveUrl(registered));
            _.WithRequestHeader(HeaderNames.IfNoneMatch, "\"2\"");
            _.StatusCodeShouldBe(304);
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
    }

    [Fact]
    public async Task An_unknown_entry_is_a_party_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await GetProblemAsync(host, ReferenceUrl(Guid.CreateVersion7(), Guid.CreateVersion7(), "Retrieve"), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task Another_entry_s_reference_id_is_a_behavior_qualifier_not_found_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var problem = await GetProblemAsync(host, ReferenceUrl(registered.PartyReferenceDataDirectoryEntryId, other.ReferenceId, "Retrieve"), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
    }

    [Fact]
    public async Task A_reference_id_that_RimBank_never_issues_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await GetProblemAsync(host, ReferenceUrl(Guid.CreateVersion7().ToString(), "not-a-guid", "Retrieve"), 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").GetProperty("referenceid").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Get.Url(ReferenceUrl(Guid.CreateVersion7(), Guid.CreateVersion7(), "Retrieve"));
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }

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
