using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.7: BIAN PartyReferenceDataDirectoryEntry/Retrieve over HTTP, answered from the inline entry view with the
// entry's version as ETag; every error is Problem Details (specs/tech-stack.md §5, ADR 0024).
public sealed class RetrievePartyReferenceDataDirectoryEntryEndpointTests(PostgresqlServer server)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Retrieve_right_after_Register_answers_the_same_entry_with_the_same_ETag()
    {
        await using var host = await server.StartHostAsync();
        var (registered, registeredTag) = await RegisterAsync(host, "Organisation");

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, Url(registered.PartyReferenceDataDirectoryEntryId));
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual(registeredTag);
        });

        var response = await result.ReadAsJsonAsync<RetrievePartyReferenceDataDirectoryEntryResponse>();
        response.ShouldBe(new RetrievePartyReferenceDataDirectoryEntryResponse(
            registered.PartyReferenceDataDirectoryEntryId,
            registered.PartyType,
            registered.DirectoryEntryDateType,
            registered.DirectoryEntryDate,
            registered.DemographicsId,
            registered.ReferenceId,
            registered.BankRelationsId));
    }

    [Fact]
    public async Task An_unknown_entry_is_a_not_found_problem()
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, Url(Guid.CreateVersion7()), 404);

        problem.GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
        problem.GetProperty("type").GetString().ShouldBe("urn:rimbank:problem:party:party-not-found");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("3f2504e0-4f89-41d3-9a0c-0305e82c3301")]
    public async Task An_id_that_RimBank_never_issues_is_a_validation_problem(string id)
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, $"/PartyReferenceDataDirectory/{id}/Retrieve", 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.invalid-identifier");
        problem.GetProperty("errors").GetProperty("partyreferencedatadirectoryid").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("W/\"1\"")]
    [InlineData("*")]
    public async Task If_None_Match_naming_the_current_version_is_not_modified(string ifNoneMatch)
    {
        await using var host = await server.StartHostAsync();
        var (registered, registeredTag) = await RegisterAsync(host, "Person");

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, Url(registered.PartyReferenceDataDirectoryEntryId));
            _.WithRequestHeader(HeaderNames.IfNoneMatch, ifNoneMatch);
            _.StatusCodeShouldBe(304);
            _.Header(HeaderNames.ETag).SingleValueShouldEqual(registeredTag);
        });

        (await result.ReadAsTextAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task If_None_Match_naming_another_version_answers_the_entry()
    {
        await using var host = await server.StartHostAsync();
        var (registered, _) = await RegisterAsync(host, "Person");

        await host.Alba.Scenario(_ =>
        {
            Get(_, Url(registered.PartyReferenceDataDirectoryEntryId));
            _.WithRequestHeader(HeaderNames.IfNoneMatch, "\"0\"");
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });
    }

    [Fact]
    public async Task If_None_Match_on_an_unknown_entry_is_still_not_found()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, Url(Guid.CreateVersion7()));
            _.WithRequestHeader(HeaderNames.IfNoneMatch, "*");
            _.StatusCodeShouldBe(404);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Get.Url(Url(Guid.CreateVersion7()));
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
    }

    private static string Url(Guid entryId) => $"/PartyReferenceDataDirectory/{entryId}/Retrieve";

    private static void Get(Scenario scenario, string url)
    {
        scenario.Get.Url(url);
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
    }

    private static async Task<(RegisterPartyResponse Response, string ETag)> RegisterAsync(PartyApiHost host, string partyType)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            _.Post.Text($$"""{ "PartyType": "{{partyType}}" }""").ToUrl(RegisterPartyEndpoint.Route);
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
            _.WithRequestHeader("Idempotency-Key", Guid.CreateVersion7().ToString());
            _.StatusCodeShouldBeOk();
        });

        var response = await result.ReadAsJsonAsync<RegisterPartyResponse>();
        return (response.ShouldNotBeNull(), result.Context.Response.Headers.ETag.ToString());
    }

    private static async Task<JsonElement> ProblemAsync(PartyApiHost host, string url, int status)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Get(_, url);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(ProblemJson);
        });

        var problem = await result.ReadAsJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(status);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return problem;
    }
}
