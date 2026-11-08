using Alba;
using Microsoft.Net.Http.Headers;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// The requests the Reference endpoint tests share (roadmap 1.10, 1.18 and 1.19). Register, Put and the Problem Details checks are the
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

    public const string AddressesAndContactPoints =
        """
        {
          "ResidentialAddress": {
            "StreetName": " Rue de Rivoli ",
            "StreetBuildingIdentification": "12",
            "PostCodeIdentification": "75001",
            "TownName": "Paris",
            "Country": { "Code": "fr" }
          },
          "CorporateAddress": {
            "Department": "Finance",
            "StreetName": "Avenue Louise",
            "StreetBuildingIdentification": "54",
            "PostCodeIdentification": "1050",
            "TownName": "Ixelles",
            "Country": { "Code": "BE" }
          },
          "eMailAddress": { "EmailAddress": " jeanne@example.fr " },
          "CellPhoneNumber": { "MobileNumber": "+33 6 12 34 56 78" },
          "SocialNetworkContacts": { "URLAddress": "https://www.linkedin.com/in/jdupont" }
        }
        """;

    public const string LegalStandingAndDocuments =
        """
        {
          "PartyLegalStructure": "private limited company",
          "PoliticalExposureType": "Politicalexposuredomestic",
          "PoliticalExposureDescriptionRecord": " Member of a regional council since 2021 ",
          "CustomerSinceDate": "2015-03-02T10:00:00+01:00",
          "GovernmentIssuedDocumentDetails": {
            "PartyDocumentType": "PersonalDocument",
            "DocumentReference": {
              "DocumentIdentification": { "IdentifierValue": "23AB45678" },
              "DocumentName": "Passport",
              "DocumentValidityPeriod": {
                "FromDateTime": { "DateTimeContent": "2023-04-01" },
                "ToDateTime": { "DateTimeContent": "2033-03-31T00:00:00Z" }
              }
            }
          },
          "DocumentDirectoryEntryInstanceReference": { "DocumentDirectoryEntryIdentification": " DOC-000123 " }
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

    public static PartyReferenceResponse ExpectedWithAddressesAndContactPoints(RegisterPartyResponse registered) =>
        ExpectedWithNamesAndResidency(registered) with
        {
            ResidentialAddress = new PostalAddressModel
            {
                StreetName = "Rue de Rivoli",
                StreetBuildingIdentification = "12",
                PostCodeIdentification = "75001",
                TownName = "Paris",
                Country = new CountryModel { Code = "FR" },
            },
            CorporateAddress = new PostalAddressModel
            {
                Department = "Finance",
                StreetName = "Avenue Louise",
                StreetBuildingIdentification = "54",
                PostCodeIdentification = "1050",
                TownName = "Ixelles",
                Country = new CountryModel { Code = "BE" },
            },
            EMailAddress = new EmailAddressModel { EmailAddress = "jeanne@example.fr" },
            CellPhoneNumber = new MobileNumberModel { MobileNumber = "+33612345678" },
            SocialNetworkContacts = new SocialNetworkContactModel { URLAddress = "https://www.linkedin.com/in/jdupont" },
        };

    public static PartyReferenceResponse ExpectedWithLegalStandingAndDocuments(RegisterPartyResponse registered) =>
        ExpectedWithAddressesAndContactPoints(registered) with
        {
            PartyLegalStructure = "PrivateLimitedCompany",
            PoliticalExposureType = "Politicalexposuredomestic",
            PoliticalExposureDescriptionRecord = "Member of a regional council since 2021",
            CustomerSinceDate = "2015-03-02",
            GovernmentIssuedDocumentDetails = new PartyDocumentModel
            {
                PartyDocumentType = "PersonalDocument",
                DocumentReference = new DocumentModel
                {
                    DocumentIdentification = new IdentifierModel { IdentifierValue = "23AB45678" },
                    DocumentName = "Passport",
                    DocumentValidityPeriod = new DateTimePeriodModel
                    {
                        FromDateTime = new DateTimeModel { DateTimeContent = "2023-04-01" },
                        ToDateTime = new DateTimeModel { DateTimeContent = "2033-03-31" },
                    },
                },
            },
            DocumentDirectoryEntryInstanceReference = new DocumentDirectoryEntryModel { DocumentDirectoryEntryIdentification = "DOC-000123" },
        };
}
