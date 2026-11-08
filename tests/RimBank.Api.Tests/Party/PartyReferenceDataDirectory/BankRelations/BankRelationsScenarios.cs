using System.Text.Json;
using Alba;
using Microsoft.Net.Http.Headers;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// The requests the BankRelations endpoint tests share (roadmap 1.12). Register, Put and the Problem Details checks are
// the Demographics ones, Get is the Associations one.
internal static class BankRelationsScenarios
{
    public static string BankRelationsUrl(Guid entryId, Guid bankRelationsId, string operation) =>
        BankRelationsUrl(entryId.ToString(), bankRelationsId.ToString(), operation);

    public static string BankRelationsUrl(string entryId, string bankRelationsId, string operation) =>
        $"/PartyReferenceDataDirectory/{entryId}/BankRelations/{bankRelationsId}/{operation}";

    public static string BankRelationsUpdateUrl(RegisterPartyResponse registered) =>
        BankRelationsUrl(registered.PartyReferenceDataDirectoryEntryId, registered.BankRelationsId, "Update");

    public static string BankRelationsRetrieveUrl(RegisterPartyResponse registered) =>
        BankRelationsUrl(registered.PartyReferenceDataDirectoryEntryId, registered.BankRelationsId, "Retrieve");

    public static string ManagedBody(Guid employeeId, string type = "relationshipmanager") =>
        $$"""
        {
          "BankRelationType": "{{type}}",
          "BusinessUnitEmployeeReference": { "PartyReference": "{{employeeId}}" },
          "BankRelationReference": {
            "RelationshipStartDate": { "DateTimeContent": "2024-01-01" },
            "RelationshipLifecycleStatus": { "StatusDescription": "active" }
          },
          "NotABianField": "ignored"
        }
        """;

    public static BankRelationsResponse ManagedResponse(RegisterPartyResponse registered, Guid employeeId) =>
        new(
            registered.BankRelationsId,
            registered.PartyReferenceDataDirectoryEntryId,
            "RelationshipManager",
            new InvolvedPartyModel { PartyReference = employeeId.ToString() },
            new BankRelationReferenceModel
            {
                RelationshipStartDate = new DateTimeModel { DateTimeContent = "2024-01-01T00:00:00Z" },
                RelationshipLifecycleStatus = new RelationshipLifecycleStatusModel { StatusDescription = "Active" },
            });

    public static async Task<BankRelationsResponse> PutBankRelationsOkAsync(PartyApiHost host, string url, string body, string ifMatch, string etag)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            DemographicsScenarios.Put(_, url, body, ifMatch);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual(etag);
        });

        return (await result.ReadAsJsonAsync<BankRelationsResponse>()).ShouldNotBeNull();
    }

    public static async Task<JsonElement> GetBankRelationsProblemAsync(PartyApiHost host, string url, int status)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            AssociationScenarios.Get(_, url);
            _.StatusCodeShouldBe(status);
            _.ContentTypeShouldBe(DemographicsScenarios.ProblemJson);
        });

        return await DemographicsScenarios.ProblemOf(result, status);
    }

    public static string[] ErrorFields(JsonElement problem) =>
        [.. problem.GetProperty("errors").EnumerateObject().Select(field => field.Name)];
}
