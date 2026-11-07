using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

public sealed class RegisterPartyEndpointTests(PostgresqlServer server)
{
    private const string Register = "/PartyReferenceDataDirectory/Register";
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Register_answers_the_new_entry_with_its_version_as_ETag()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            Post(_, """{ "PartyType": "Person" }""", key: "register-1");
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });

        var response = await result.ReadAsJsonAsync<RegisterPartyResponse>();
        response.ShouldNotBeNull();
        response.PartyType.ShouldBe("Person");
        response.DirectoryEntryDateType.ShouldBe("OpenDate");
        response.DirectoryEntryDate.ShouldBe(PostgresqlServer.Now);
        new[] { response.PartyReferenceDataDirectoryEntryId, response.DemographicsId, response.ReferenceId, response.BankRelationsId }
            .ShouldAllBe(id => id != Guid.Empty && id.Version == 7);
        (await host.ScalarAsync<string>(
                $"SELECT type FROM party_reference_data_directory.mt_events WHERE stream_id = '{response.PartyReferenceDataDirectoryEntryId}'"))
            .ShouldBe("party_registered");
    }

    [Fact]
    public async Task The_same_key_and_body_replay_the_first_answer()
    {
        await using var host = await server.StartHostAsync();
        const string body = """{ "PartyType": "Organisation" }""";

        var first = await host.Alba.Scenario(_ => Post(_, body, key: "register-twice"));
        var second = await host.Alba.Scenario(_ =>
        {
            Post(_, body, key: "register-twice");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });

        (await second.ReadAsTextAsync()).ShouldBe(await first.ReadAsTextAsync());
        (await host.ScalarAsync<long>(PartyApiHost.Streams)).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_with_another_body_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        await host.Alba.Scenario(_ => Post(_, """{ "PartyType": "Person" }""", key: "register-conflict"));

        var problem = await ProblemAsync(host, """{ "PartyType": "Organisation" }""", 409, key: "register-conflict");

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-reused");
        (await host.ScalarAsync<long>(PartyApiHost.Streams)).ShouldBe(1);
    }

    [Fact]
    public async Task A_missing_idempotency_key_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, """{ "PartyType": "Person" }""", 400, key: null);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-missing");
        await NothingStoredAsync(host);
    }

    [Fact]
    public async Task An_invalid_idempotency_key_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, """{ "PartyType": "Person" }""", 400, key: new string('k', 256));

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.idempotency-key-invalid");
        await NothingStoredAsync(host);
    }

    [Theory]
    [InlineData("""{ }""", "party.party-type-required")]
    [InlineData("""{ "PartyType": null }""", "party.party-type-required")]
    [InlineData("""{ "PartyType": "" }""", "party.party-type-required")]
    [InlineData("""{ "PartyType": "Alien" }""", "party.party-type-invalid")]
    [InlineData("""{ "PartyType": "person" }""", "party.party-type-invalid")]
    public async Task A_missing_or_unknown_party_type_is_a_validation_problem(string body, string errorCode)
    {
        await using var host = await server.StartHostAsync();

        var problem = await ProblemAsync(host, body, 400);

        problem.GetProperty("errorCode").GetString().ShouldBe(errorCode);
        problem.GetProperty("type").GetString().ShouldBe($"urn:rimbank:problem:party:{errorCode["party.".Length..]}");
        problem.GetProperty("errors").GetProperty("PartyType").GetArrayLength().ShouldBe(1);
        await NothingStoredAsync(host);
    }

    [Theory]
    [InlineData("Test", """{ "PartyType": """)]
    [InlineData("Test", """{ "PartyType": 1 }""")]
    [InlineData("Test", "")]
    [InlineData("Development", """{ "PartyType": """)]
    [InlineData("Development", "")]
    public async Task A_body_that_is_not_a_register_request_is_a_bad_request(string environment, string body)
    {
        await using var host = await server.StartHostAsync(environment);

        var problem = await ProblemAsync(host, body, 400);

        problem.GetProperty("errorCode").GetString().ShouldBe("shared.request-malformed");
        await NothingStoredAsync(host);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            _.Post.Text("""{ "PartyType": "Person" }""").ToUrl(Register);
            _.WithRequestHeader(HeaderNames.ContentType, "application/json");
            _.WithRequestHeader("Idempotency-Key", "anonymous");
            _.StatusCodeShouldBe(401);
            _.ContentTypeShouldBe(ProblemJson);
        });

        (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated");
        await NothingStoredAsync(host);
    }

    private static void Post(Scenario scenario, string body, string? key)
    {
        scenario.Post.Text(body).ToUrl(Register);
        scenario.WithRequestHeader(HeaderNames.ContentType, "application/json");
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
        if (key is not null)
        {
            scenario.WithRequestHeader("Idempotency-Key", key);
        }
    }

    private static async Task<JsonElement> ProblemAsync(PartyApiHost host, string body, int status, string? key = "register-problem")
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Post(_, body, key);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(ProblemJson);
        });

        var problem = await result.ReadAsJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(status);
        problem.GetProperty("type").GetString().ShouldStartWith("urn:rimbank:problem:");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return problem;
    }

    private static async Task NothingStoredAsync(PartyApiHost host)
    {
        (await host.ScalarAsync<long>(PartyApiHost.Streams)).ShouldBe(0);
        (await host.ScalarAsync<long>(PartyApiHost.Outcomes)).ShouldBe(0);
    }
}
