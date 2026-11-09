using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.15: every BIAN operation of the context is authorised by its own policy. Commands need the write scope and
// queries the read scope, and a token with only the other one is a 403 shared.forbidden problem that comes before every
// other check of the endpoint (ADR 0038).
public sealed class AuthorisationTests(PostgresqlServer server)
{
    private const string Read = PartyReferenceDataDirectoryModule.ReadScope;
    private const string Write = PartyReferenceDataDirectoryModule.WriteScope;
    private const string ProblemJson = "application/problem+json";

    // Each operation with its verb and a route to unknown records. The command is sent without a body,
    // Idempotency-Key or If-Match, and the query asks for a record that does not exist, so a 403 proves that
    // authorisation comes before the 400, 428 and 404 the endpoint would otherwise answer.
    public static TheoryData<string, string, string, string> Operations
    {
        get
        {
            var entry = Guid.CreateVersion7();
            var record = Guid.CreateVersion7();
            var data = new TheoryData<string, string, string, string>
            {
                { RegisterPartyEndpoint.OperationId, "POST", "/PartyReferenceDataDirectory/Register", Read },
                { RetrievePartyReferenceDataDirectoryEntryEndpoint.OperationId, "GET", $"/PartyReferenceDataDirectory/{entry}/Retrieve", Write },
                { UpdatePartyReferenceDataDirectoryEntryEndpoint.OperationId, "PUT", $"/PartyReferenceDataDirectory/{entry}/Update", Read },
                { UpdateDemographicsEndpoint.OperationId, "PUT", $"/PartyReferenceDataDirectory/{entry}/Demographics/{record}/Update", Read },
                { ExchangeDemographicsEndpoint.OperationId, "PUT", $"/PartyReferenceDataDirectory/{entry}/Demographics/{record}/Exchange", Read },
                { RetrieveDemographicsEndpoint.OperationId, "GET", $"/PartyReferenceDataDirectory/{entry}/Demographics/{record}/Retrieve", Write },
                { UpdatePartyReferenceEndpoint.OperationId, "PUT", $"/PartyReferenceDataDirectory/{entry}/Reference/{record}/Update", Read },
                { RetrievePartyReferenceEndpoint.OperationId, "GET", $"/PartyReferenceDataDirectory/{entry}/Reference/{record}/Retrieve", Write },
                { RegisterPartyAssociationEndpoint.OperationId, "POST", $"/PartyReferenceDataDirectory/{entry}/Associations/Register", Read },
                { UpdatePartyAssociationEndpoint.OperationId, "PUT", $"/PartyReferenceDataDirectory/{entry}/Associations/{record}/Update", Read },
                { RetrievePartyAssociationEndpoint.OperationId, "GET", $"/PartyReferenceDataDirectory/{entry}/Associations/{record}/Retrieve", Write },
                { UpdateBankRelationsEndpoint.OperationId, "PUT", $"/PartyReferenceDataDirectory/{entry}/BankRelations/{record}/Update", Read },
                { RetrieveBankRelationsEndpoint.OperationId, "GET", $"/PartyReferenceDataDirectory/{entry}/BankRelations/{record}/Retrieve", Write },
            };
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task A_token_with_only_the_other_kinds_scope_is_forbidden_before_any_other_check(
        string operationId, string method, string url, string otherScope)
    {
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            Send(_, method, url, TestTokens.Issue(scopes: [otherScope]));
            _.StatusCodeShouldBe(403);
            _.ContentTypeShouldBe(ProblemJson);
        });

        using var problem = JsonDocument.Parse(await result.ReadAsTextAsync());
        problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("shared.forbidden", operationId);
        problem.RootElement.GetProperty("instance").GetString().ShouldBe(url);
        (await host.ScalarAsync<long>(PartyApiHost.Streams)).ShouldBe(0);
        (await host.ScalarAsync<long>(PartyApiHost.Outcomes)).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task A_token_with_no_scope_is_forbidden(string operationId, string method, string url, string otherScope)
    {
        _ = otherScope;
        await using var host = await server.StartHostAsync();

        await host.Alba.Scenario(_ =>
        {
            Send(_, method, url, TestTokens.Issue(scopes: []));
            _.StatusCodeShouldBe(403);
        });

        (await host.ScalarAsync<long>(PartyApiHost.Streams)).ShouldBe(0, operationId);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task A_caller_without_a_token_is_unauthenticated(string operationId, string method, string url, string otherScope)
    {
        _ = otherScope;
        await using var host = await server.StartHostAsync();

        var result = await host.Alba.Scenario(_ =>
        {
            Send(_, method, url, token: null);
            _.StatusCodeShouldBe(401);
        });

        using var problem = JsonDocument.Parse(await result.ReadAsTextAsync());
        problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("shared.unauthenticated", operationId);
    }

    [Fact]
    public async Task The_write_scope_alone_registers_and_the_read_scope_alone_retrieves()
    {
        await using var host = await server.StartHostAsync();

        var registered = await host.Alba.Scenario(_ =>
        {
            _.Post.Json(new { PartyType = "Person" }).ToUrl("/PartyReferenceDataDirectory/Register");
            _.WithRequestHeader("Idempotency-Key", "authorised-register");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue(scopes: [Write]));
            _.StatusCodeShouldBeOk();
        });
        var entry = (await registered.ReadAsJsonAsync<RegisterPartyResponse>()).ShouldNotBeNull().PartyReferenceDataDirectoryEntryId;

        await host.Alba.Scenario(_ =>
        {
            _.Get.Url($"/PartyReferenceDataDirectory/{entry}/Retrieve");
            _.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue(scopes: [Read]));
            _.StatusCodeShouldBeOk();
        });
    }

    private static void Send(Scenario scenario, string method, string url, string? token)
    {
        _ = method switch
        {
            "GET" => scenario.Get.Url(url),
            "POST" => scenario.Post.Url(url),
            "PUT" => scenario.Put.Url(url),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Only GET, POST and PUT are mapped."),
        };

        if (token is not null)
        {
            scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + token);
        }
    }
}
