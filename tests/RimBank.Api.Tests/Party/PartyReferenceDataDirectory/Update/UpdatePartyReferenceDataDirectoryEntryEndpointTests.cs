using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.8: BIAN PartyReferenceDataDirectoryEntry/Update over HTTP. It is a command with an Idempotency-Key and the
// entry's version in If-Match, answered from its acknowledgement with the new version as ETag; every error is Problem
// Details (specs/tech-stack.md §5, ADR 0024, ADR 0027).
public sealed class UpdatePartyReferenceDataDirectoryEntryEndpointTests(PostgresqlServer server)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Updating_with_the_current_version_answers_the_updated_entry_and_Retrieve_shows_it()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");

        var result = await host.Alba.Scenario(_ =>
        {
            Put(_, registered.PartyReferenceDataDirectoryEntryId, """{ "PartyType": "Organisation" }""", ifMatch: "\"1\"");
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        var updated = (await result.ReadAsJsonAsync<UpdatePartyReferenceDataDirectoryEntryResponse>()).ShouldNotBeNull();
        updated.ShouldBe(new UpdatePartyReferenceDataDirectoryEntryResponse(
            registered.PartyReferenceDataDirectoryEntryId,
            "Organisation",
            "OpenDate",
            registered.DirectoryEntryDate,
            updated.RefreshDate,
            registered.DemographicsId,
            registered.ReferenceId,
            registered.BankRelationsId));
        updated.RefreshDate.ShouldBeGreaterThanOrEqualTo(registered.DirectoryEntryDate);

        var retrieved = await host.Alba.Scenario(_ =>
        {
            _.Get.Url($"/PartyReferenceDataDirectory/{registered.PartyReferenceDataDirectoryEntryId}/Retrieve");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
        var view = (await retrieved.ReadAsJsonAsync<RetrievePartyReferenceDataDirectoryEntryResponse>()).ShouldNotBeNull();
        view.PartyType.ShouldBe("Organisation");
        view.RefreshDate.ShouldBe(updated.RefreshDate);
    }

    [Fact]
    public async Task An_empty_body_keeps_the_party_type_and_records_the_refresh()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Organisation");

        var result = await host.Alba.Scenario(_ =>
        {
            Put(_, registered.PartyReferenceDataDirectoryEntryId, "{}", ifMatch: "\"1\"");
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });

        (await result.ReadAsJsonAsync<UpdatePartyReferenceDataDirectoryEntryResponse>()).ShouldNotBeNull().PartyType.ShouldBe("Organisation");
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_is_a_conflict_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");
        await host.Alba.Scenario(_ =>
        {
            Put(_, registered.PartyReferenceDataDirectoryEntryId, "{}", ifMatch: "\"1\"");
            _.StatusCodeShouldBeOk();
        });

        var problem = await ProblemAsync(host, registered.PartyReferenceDataDirectoryEntryId, """{ "PartyType": "Organisation" }""", "\"1\"", 409);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.version-conflict");
        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:party:version-conflict");
    }

    [Fact]
    public async Task A_missing_If_Match_is_a_precondition_required_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");

        var problem = await ProblemAsync(host, registered.PartyReferenceDataDirectoryEntryId, "{}", ifMatch: null, 428);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-required");
    }

    [Theory]
    [InlineData("W/\"1\"")]
    [InlineData("*")]
    [InlineData("\"1\", \"2\"")]
    [InlineData("1")]
    public async Task An_If_Match_that_is_not_one_strong_version_is_a_validation_problem(string ifMatch)
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");

        var problem = await ProblemAsync(host, registered.PartyReferenceDataDirectoryEntryId, "{}", ifMatch, 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.if-match-invalid");
        problem.GetProperty("errors").GetProperty("If-Match").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData("person")]
    [InlineData("")]
    public async Task A_party_type_that_is_not_a_BIAN_one_is_a_validation_problem(string partyType)
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");

        var problem = await ProblemAsync(host, registered.PartyReferenceDataDirectoryEntryId, $$"""{ "PartyType": "{{partyType}}" }""", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-type-invalid");
        problem.GetProperty("errors").GetProperty("PartyType").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task An_unknown_entry_is_a_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, Guid.CreateVersion7(), "{}", "\"1\"", 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("3f2504e0-4f89-41d3-9a0c-0305e82c3301")]
    public async Task An_id_that_RimBank_never_issues_is_a_validation_problem(string id)
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, $"/PartyReferenceDataDirectory/{id}/Update", "{}", "\"1\"", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").GetProperty("partyreferencedatadirectoryid").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_missing_Idempotency_Key_is_a_validation_problem()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(Url(registered.PartyReferenceDataDirectoryEntryId));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(400);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-missing");
    }

    [Fact]
    public async Task The_same_key_and_request_replays_the_first_answer_and_another_If_Match_is_an_idempotency_conflict()
    {
        await using var host = await server.StartHostAsync();
        var registered = await RegisterAsync(host, "Person");
        var key = Guid.CreateVersion7().ToString();

        var first = await host.Alba.Scenario(_ =>
        {
            Put(_, registered.PartyReferenceDataDirectoryEntryId, """{ "PartyType": "Organisation" }""", "\"1\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
        var again = await host.Alba.Scenario(_ =>
        {
            Put(_, registered.PartyReferenceDataDirectoryEntryId, """{ "PartyType": "Organisation" }""", "\"1\"", key);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"2\"");
        });
        var changed = await host.Alba.Scenario(_ =>
        {
            Put(_, registered.PartyReferenceDataDirectoryEntryId, """{ "PartyType": "Organisation" }""", "\"2\"", key);
            _.StatusCodeShouldBe(409);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await again.ReadAsTextAsync()).ShouldBe(await first.ReadAsTextAsync());
        (await changed.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-reused");
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Put.Text("{}").ToUrl(Url(Guid.CreateVersion7()));
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader("Idempotency-Key", Guid.CreateVersion7().ToString());
            _.WithRequestHeader(HeaderNames.IfMatch, "\"1\"");
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }

    private static string Url(Guid entryId) => $"/PartyReferenceDataDirectory/{entryId}/Update";

    private static void Put(Scenario scenario, Guid entryId, string body, string? ifMatch, string? key = null) =>
        Put(scenario, Url(entryId), body, ifMatch, key);

    private static void Put(Scenario scenario, string url, string body, string? ifMatch, string? key = null)
    {
        scenario.Put.Text(body).ToUrl(url);
        scenario.WithRequestHeader(HeaderNames.ContentType, "application/json");
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
        scenario.WithRequestHeader("Idempotency-Key", key ?? Guid.CreateVersion7().ToString());
        if (ifMatch is not null)
        {
            scenario.WithRequestHeader(HeaderNames.IfMatch, ifMatch);
        }
    }

    private static async Task<RegisterPartyResponse> RegisterAsync(PartyApiHost host, string partyType)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            _.Post.Text($$"""{ "PartyType": "{{partyType}}" }""").ToUrl(RegisterPartyEndpoint.Route);
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.WithRequestHeader("Idempotency-Key", Guid.CreateVersion7().ToString());
            _.StatusCodeShouldBeOk();
        });

        return (await result.ReadAsJsonAsync<RegisterPartyResponse>()).ShouldNotBeNull();
    }

    private static Task<JsonElement> ProblemAsync(PartyApiHost host, Guid entryId, string body, string? ifMatch, int status) =>
        ProblemAsync(host, Url(entryId), body, ifMatch, status);

    private static async Task<JsonElement> ProblemAsync(PartyApiHost host, string url, string body, string? ifMatch, int status)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Put(_, url, body, ifMatch);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(ProblemJson);
        });

        var problem = await result.ReadAsJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(status);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return problem;
    }
}
