using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;
using RimBank.Host.Testing;
using static RimBank.Party.PartyReferenceDataDirectory.Api.Tests.DemographicsScenarios;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// The requests the Associations endpoint tests share (roadmap 1.11). Register, Put and the Problem Details checks are the
// Demographics ones.
internal static class AssociationScenarios
{
    public static string RegisterUrl(Guid ownerId) => RegisterUrl(ownerId.ToString());

    public static string RegisterUrl(string ownerId) => $"/PartyReferenceDataDirectory/{ownerId}/Associations/Register";

    public static string AssociationUrl(Guid ownerId, Guid associationId, string operation) =>
        AssociationUrl(ownerId.ToString(), associationId.ToString(), operation);

    public static string AssociationUrl(string ownerId, string associationId, string operation) =>
        $"/PartyReferenceDataDirectory/{ownerId}/Associations/{associationId}/{operation}";

    public static string RegisterBody(Guid associateId, string type = "partyisparentofparty") =>
        $$"""
        {
          "AssociateReference": { "PartyReference": "{{associateId}}" },
          "AssociateType": "{{type}}",
          "AssociationValidFromToDate": { "FromDateTime": "2024-01-01" },
          "NotABianField": "ignored"
        }
        """;

    public static void Post(Scenario scenario, string url, string body, string? key = null)
    {
        scenario.Post.Text(body).ToUrl(url);
        scenario.WithRequestHeader(HeaderNames.ContentType, "application/json");
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
        if (key is not "")
        {
            scenario.WithRequestHeader("Idempotency-Key", key ?? Guid.CreateVersion7().ToString());
        }
    }

    public static void Get(Scenario scenario, string url, string? ifNoneMatch = null)
    {
        scenario.Get.Url(url);
        scenario.WithRequestHeader(HeaderNames.Authorization, "Bearer " + TestTokens.Issue());
        if (ifNoneMatch is not null)
        {
            scenario.WithRequestHeader(HeaderNames.IfNoneMatch, ifNoneMatch);
        }
    }

    // Registers two parties and associates the second with the first.
    public static async Task<(RegisterPartyResponse Owner, RegisterPartyResponse Associate, PartyAssociationResponse Association)>
        AssociateAsync(PartyApiHost host)
    {
        var owner = await RegisterAsync(host);
        var associate = await RegisterAsync(host);
        var association = await PostOkAsync(host, RegisterUrl(owner.PartyReferenceDataDirectoryEntryId), RegisterBody(associate.PartyReferenceDataDirectoryEntryId));
        return (owner, associate, association);
    }

    public static async Task<PartyAssociationResponse> PostOkAsync(PartyApiHost host, string url, string body)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Post(_, url, body);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual("\"1\"");
        });

        return (await result.ReadAsJsonAsync<PartyAssociationResponse>()).ShouldNotBeNull();
    }

    public static async Task<JsonElement> PostProblemAsync(PartyApiHost host, string url, string body, int status)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Post(_, url, body);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(ProblemJson);
        });

        return await ProblemOf(result, status);
    }

    public static async Task<PartyAssociationResponse> PutAssociationOkAsync(
        PartyApiHost host,
        string url,
        string body,
        string ifMatch,
        string etag)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            Put(_, url, body, ifMatch);
            _.StatusCodeShouldBeOk();
            _.Header(HeaderNames.ETag).SingleValueShouldEqual(etag);
        });

        return (await result.ReadAsJsonAsync<PartyAssociationResponse>()).ShouldNotBeNull();
    }
}
