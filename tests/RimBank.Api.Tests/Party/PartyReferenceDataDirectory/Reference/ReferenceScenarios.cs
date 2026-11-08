using Alba;
using Microsoft.Net.Http.Headers;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// The requests the Reference endpoint tests share (roadmap 1.10). Register, Put and the Problem Details checks are the
// Demographics ones.
internal static class ReferenceScenarios
{
    public const string Lei = "5493001KJTIIGC8Y1R12";

    public const string Identifiers =
        """
        {
          "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": " FR12345678901 ", "NationalRegistrationNumber": "185057800608436" },
          "PartyLegalEntityReference": { "Identification": "5493001kjtiigc8y1r12" }
        }
        """;

    public const string NamesAndResidency =
        """
        {
          "PartyFullLegalName": " Jeanne Marie Dupont ",
          "PartyNameSalutation": "Ms",
          "DateofBirth": "1990-05-17",
          "Nationality": { "Code": "fr" },
          "PartyCountryOfResidence": { "Code": "BE" },
          "ResidencyStatus": { "StatusDescription": "nonresident", "StatusDateTime": "2024-01-01T00:00:00Z" }
        }
        """;

    public static string ReferenceUrl(Guid entryId, Guid referenceId, string operation) =>
        ReferenceUrl(entryId.ToString(), referenceId.ToString(), operation);

    public static string ReferenceUrl(string entryId, string referenceId, string operation) =>
        $"/PartyReferenceDataDirectory/{entryId}/Reference/{referenceId}/{operation}";

    public static string UpdateUrl(RegisterPartyResponse registered) =>
        ReferenceUrl(registered.PartyReferenceDataDirectoryEntryId, registered.ReferenceId, "Update");

    public static string RetrieveUrl(RegisterPartyResponse registered) =>
        ReferenceUrl(registered.PartyReferenceDataDirectoryEntryId, registered.ReferenceId, "Retrieve");

    public static async Task<PartyReferenceResponse> PutReferenceOkAsync(PartyApiHost host, string url, string body, string ifMatch, string etag)
    {
        var result = await host.Alba.Scenario(_ =>
        {
            DemographicsScenarios.Put(_, url, body, ifMatch);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json; charset=utf-8");
            _.Header(HeaderNames.ETag).SingleValueShouldEqual(etag);
        });

        return (await result.ReadAsJsonAsync<PartyReferenceResponse>()).ShouldNotBeNull();
    }

    public static PartyReferenceResponse Expected(RegisterPartyResponse registered) =>
        new(
            registered.PartyReferenceDataDirectoryEntryId,
            registered.ReferenceId,
            new GovernmentIssuedIdentityModel { TaxIdentificationNumber = "FR12345678901", NationalRegistrationNumber = "185057800608436" },
            new PartyLegalEntityModel { Identification = Lei });

    public static PartyReferenceResponse ExpectedWithNamesAndResidency(RegisterPartyResponse registered) =>
        Expected(registered) with
        {
            PartyFullLegalName = "Jeanne Marie Dupont",
            PartyNameSalutation = "Ms",
            DateofBirth = "1990-05-17",
            Nationality = new CountryModel { Code = "FR" },
            PartyCountryOfResidence = new CountryModel { Code = "BE" },
            ResidencyStatus = new ResidencyStatusModel { StatusDescription = "NonResident", StatusDateTime = "2024-01-01T00:00:00+00:00" },
        };
}
