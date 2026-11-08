using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// The requests the Demographics endpoint tests share (roadmap 1.9).
internal static class DemographicsScenarios
{
    public const string ProblemJson = "application/problem+json";

    public const string Details =
        """
        {
          "Socio-EconomicClassification": "B2",
          "EthnicityReligion": "never stored",
          "Employment": { "PartyReference": "Contoso Ltd", "InvolvementReference": "Accountant" },
          "EmploymentHistory": "Contoso Ltd since 2020",
          "EducationHistory": "MSc Finance",
          "ServicingConstraints": "Large print"
        }
        """;

    public static string Url(Guid entryId, Guid demographicsId, string operation) =>
        Url(entryId.ToString(), demographicsId.ToString(), operation);

    public static string Url(string entryId, string demographicsId, string operation) =>
        $"/PartyReferenceDataDirectory/{entryId}/Demographics/{demographicsId}/{operation}";

    public static async Task<RegisterPartyResponse> RegisterAsync(PartyApiHost host, string partyType = "Person")
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

    public static void Put(Scenario scenario, string url, string body, string? ifMatch, string? key = null)
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

    public static async Task<DemographicsResponse> PutOkAsync(PartyApiHost host, string url, string body, string ifMatch, string etag)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Put(_, url, body, ifMatch);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual(etag);
        });

        return (await result.ReadAsJsonAsync<DemographicsResponse>()).ShouldNotBeNull();
    }

    public static async Task<JsonElement> PutProblemAsync(PartyApiHost host, string url, string body, string? ifMatch, int status)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Put(_, url, body, ifMatch);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(ProblemJson);
        });

        return await ProblemOf(result, status);
    }

    public static async Task<JsonElement> ProblemOf(IScenarioResult result, int status)
    {
        var problem = await result.ReadAsJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(status);
        problem.GetProperty("type").GetString().ShouldStartWith("urn:rimbank:problem:");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return problem;
    }
}
