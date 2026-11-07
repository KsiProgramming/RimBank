using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.9: BIAN Demographics/Update over HTTP. A partial update with an Idempotency-Key and the entry's version in
// If-Match, answered from its acknowledgement with the new version as ETag; every error is Problem Details
// (specs/tech-stack.md §5, ADR 0024, ADR 0027).
public sealed class UpdateDemographicsEndpointTests(PostgresqlServer server)
{
    [Fact]
    public async Task Updating_with_the_current_version_answers_the_unverified_details_without_EthnicityReligion()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var updated = await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");

        updated.ShouldBe(new DemographicsResponse(
            registered.PartyReferenceDataDirectoryEntryId,
            registered.DemographicsId,
            "B2",
            new DemographicsEmployment("Contoso Ltd", "Accountant"),
            "Contoso Ltd since 2020",
            "MSc Finance",
            "Large print",
            "Unverified",
            null));
    }

    [Fact]
    public async Task A_partial_update_keeps_the_fields_it_leaves_out_and_clears_the_empty_ones()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");

        var updated = await PutOkAsync(
            host,
            UpdateUrl(registered),
            """{ "EducationHistory": "PhD", "ServicingConstraints": "", "Employment": { "PartyReference": "Fabrikam" } }""",
            "\"2\"",
            "\"3\"");

        updated.SocioEconomicClassification.ShouldBe("B2");
        updated.EducationHistory.ShouldBe("PhD");
        updated.ServicingConstraints.ShouldBeNull();
        updated.Employment.ShouldBe(new DemographicsEmployment("Fabrikam", null));
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        await PutOkAsync(host, UpdateUrl(registered), Details, "\"1\"", "\"2\"");

        var problem = await PutProblemAsync(host, UpdateUrl(registered), "{}", "\"1\"", 409);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.version-conflict");
    }

    [Fact]
    public async Task A_missing_If_Match_is_a_precondition_required_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, UpdateUrl(registered), "{}", ifMatch: null, 428);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-required");
    }

    [Fact]
    public async Task A_weak_If_Match_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var problem = await PutProblemAsync(host, UpdateUrl(registered), "{}", "W/\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-invalid");
    }

    [Fact]
    public async Task Texts_that_are_too_long_are_a_validation_problem_naming_each_field()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Socio-EconomicClassification"] = new('a', 141),
            ["EducationHistory"] = new('b', 2049),
        });

        var problem = await PutProblemAsync(host, UpdateUrl(registered), body, "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.demographics-invalid");
        problem.GetProperty("errors").EnumerateObject().Select(field => field.Name)
            .ShouldBe(["Socio-EconomicClassification", "EducationHistory"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_unknown_entry_is_a_party_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await PutProblemAsync(host, Url(Guid.CreateVersion7(), Guid.CreateVersion7(), "Update"), "{}", "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task Another_entry_s_demographics_id_is_a_behavior_qualifier_not_found_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var other = await RegisterAsync(host);

        var problem = await PutProblemAsync(
            host, Url(registered.PartyReferenceDataDirectoryEntryId, other.DemographicsId, "Update"), "{}", "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.behavior-qualifier-not-found");
    }

    [Theory]
    [InlineData("not-a-guid", "0198f0a2-5b1c-7000-8000-000000000001", "partyreferencedatadirectoryid")]
    [InlineData("0198f0a2-5b1c-7000-8000-000000000001", "not-a-guid", "demographicsid")]
    [InlineData("0198f0a2-5b1c-7000-8000-000000000001", "3f2504e0-4f89-41d3-9a0c-0305e82c3301", "demographicsid")]
    public async Task An_id_that_RimBank_never_issues_is_a_validation_problem_naming_it(string entryId, string demographicsId, string field)
    {
        await using var host = await server.StartHostAsync();

        var problem = await PutProblemAsync(host, Url(entryId, demographicsId, "Update"), "{}", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").GetProperty(field).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_missing_Idempotency_Key_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(UpdateUrl(registered));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(400);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-missing");
    }

    [Fact]
    public async Task The_same_key_and_request_replays_the_first_answer()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host);
        var key = Guid.CreateVersion7().ToString();

        var first = await host.Alba.Scenario(_ =>
        {
            Put(_, UpdateUrl(registered), Details, "\"1\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
        var again = await host.Alba.Scenario(_ =>
        {
            Put(_, UpdateUrl(registered), Details, "\"1\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        (await again.ReadAsTextAsync()).ShouldBe(await first.ReadAsTextAsync());
        (await host.ScalarAsync<long>(
            $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{registered.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe(2);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(Url(Guid.CreateVersion7(), Guid.CreateVersion7(), "Update"));
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
}
