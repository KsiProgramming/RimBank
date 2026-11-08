using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.10, 1.18 and 1.19: the BIAN Reference Update body becomes its command, with the route's ids and the If-Match
// version; the Reference fields RimBank does not record yet are found and refused (ADR 0024, ADR 0029).
public sealed class PartyReferenceRequestTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.From("reference-1");
    private static readonly PartyReferenceDataDirectoryEntryId _entryId = PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7());
    private static readonly PartyReferenceId _referenceId = PartyReferenceId.From(Guid.CreateVersion7());

    [Fact]
    public void The_BIAN_identifier_fields_become_the_changes()
    {
        var command = Read(
            """
            {
              "GovernmentIssuedIdentityReference": { "TaxIdentificationNumber": "FR12345678901", "NationalRegistrationNumber": "" },
              "PartyLegalEntityReference": { "Identification": "5493001KJTIIGC8Y1R12" }
            }
            """).ToCommand(_entryId, _referenceId, 4, _key).Value;

        command.ShouldBe(new UpdatePartyReference(
            _entryId,
            _referenceId,
            4,
            new PartyReferenceChanges(
                new PartyIdentifierChange<TaxIdentificationNumber>(TaxIdentificationNumber.TryCreate("FR12345678901").Value),
                new PartyIdentifierChange<NationalRegistrationNumber>(null),
                new PartyIdentifierChange<LegalEntityIdentifier>(LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value)),
            _key));
    }

    [Fact]
    public void The_BIAN_name_and_residency_fields_become_the_changes()
    {
        var changes = Read(
            """
            {
              "partyfulllegalname": " Jeanne Marie Dupont ",
              "PartyNameSalutation": "",
              "DateofBirth": "1990-05-17",
              "Nationality": { "Code": "fr" },
              "PartyCountryOfResidence": {},
              "ResidencyStatus": { "StatusDescription": "Resident", "StatusDateTime": "2024-01-01T00:00:00Z" }
            }
            """).ToCommand(_entryId, _referenceId, 1, _key).Value.Changes;

        changes.ShouldBe(PartyReferenceChanges.None with
        {
            PartyFullLegalName = new PartyReferenceChange<PartyFullLegalName?>(PartyFullLegalName.TryCreate("Jeanne Marie Dupont").Value),
            PartyNameSalutation = new PartyReferenceChange<PartyNameSalutation?>(null),
            DateOfBirth = new PartyReferenceChange<DateOnly?>(new DateOnly(1990, 5, 17)),
            Nationality = new PartyReferenceChange<CountryCode?>(CountryCode.From("FR")),
            PartyCountryOfResidence = new PartyReferenceChange<CountryCode?>(null),
            ResidencyStatus = new PartyReferenceChange<ResidencyStatus?>(
                new ResidencyStatus(ResidencyStatusCode.Resident, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))),
        });
    }

    [Fact]
    public void The_BIAN_address_and_contact_point_fields_become_the_changes()
    {
        var changes = Read(
            """
            {
              "ResidentialAddress": { "StreetName": "Rue de Rivoli", "TownName": "Paris", "Country": { "Code": "fr" } },
              "PartyAddress": {},
              "CorporateAddress": { "TownName": "", "Country": { "Code": "" } },
              "eMailAddress": { "EmailAddress": "jeanne@example.fr" },
              "CellPhoneNumber": {},
              "SocialNetworkContacts": { "URLAddress": "https://www.linkedin.com/in/jdupont" }
            }
            """).ToCommand(_entryId, _referenceId, 1, _key).Value.Changes;

        changes.ShouldBe(PartyReferenceChanges.None with
        {
            ResidentialAddress = new PartyReferenceChange<PostalAddress?>(
                new PostalAddress(null, null, "Rue de Rivoli", null, null, null, null, null, "Paris", CountryCode.From("FR"))),
            PartyAddress = new PartyReferenceChange<PostalAddress?>(null),
            CorporateAddress = new PartyReferenceChange<PostalAddress?>(null),
            EmailAddress = new PartyReferenceChange<EmailAddress?>(EmailAddress.TryCreate("jeanne@example.fr").Value),
            MobileNumber = new PartyReferenceChange<MobileNumber?>(null),
            SocialNetworkAddress = new PartyReferenceChange<SocialNetworkAddress?>(
                SocialNetworkAddress.TryCreate("https://www.linkedin.com/in/jdupont").Value),
        });
    }

    [Fact]
    public void Unrecorded_address_and_contact_point_parts_are_unsupported()
    {
        var request = Read(
            """
            {
              "ResidentialAddress": { "TownName": "Paris", "AddressType": "HOME", "Country": { "Code": "FR", "Citizen": "x" } },
              "CorporateAddress": { "ValidityPeriod": { "FromDateTime": "2024-01-01T00:00:00Z" }, "Block": null },
              "eMailAddress": { "EmailAddress": "jeanne@example.fr", "Identification": "x" },
              "CellPhoneNumber": { "PhoneNumber": "+33123456789" },
              "SocialNetworkContacts": { "BICAddress": "BNPAFRPP" }
            }
            """);

        request.UnsupportedFields().ShouldBe(
            [
                "ResidentialAddress.AddressType",
                "ResidentialAddress.Country.Citizen",
                "CorporateAddress.ValidityPeriod",
                "eMailAddress.Identification",
                "CellPhoneNumber.PhoneNumber",
                "SocialNetworkContacts.BICAddress",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void An_empty_body_changes_nothing() =>
        Read("{}").ToCommand(_entryId, _referenceId, 1, _key).Value.Changes.ShouldBe(PartyReferenceChanges.None);

    [Fact]
    public void Staged_fields_and_unrecorded_parts_are_unsupported_whatever_their_case()
    {
        var request = Read(
            """
            {
              "partylegalstructure": "SAS",
              "CompanyOfficerRole": { "PartyRole": "Director" },
              "GovernmentIssuedIdentityReference": { "OtherIdentification": "X1", "Declaration": null },
              "PartyLegalEntityReference": { "ContactPoint": "x" },
              "Nationality": { "Code": "FR", "Citizen": "Jane" },
              "PartyCountryOfResidence": { "Tax": "x" },
              "ResidencyStatus": { "StatusDescription": "Resident", "StatusReason": "Works abroad" }
            }
            """);

        request.UnsupportedFields().ShouldBe(
            [
                "PartyLegalStructure",
                "CompanyOfficerRole",
                "GovernmentIssuedIdentityReference.OtherIdentification",
                "PartyLegalEntityReference.ContactPoint",
                "Nationality.Citizen",
                "PartyCountryOfResidence.Tax",
                "ResidencyStatus.StatusReason",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Fields_sent_as_null_and_properties_that_are_not_BIAN_s_are_not_unsupported() =>
        Read("""{ "PartyLegalStructure": null, "Nationality": { "Citizen": null }, "NotABianField": "x" }""").UnsupportedFields().ShouldBeEmpty();

    [Fact]
    public void An_unsupported_field_makes_the_command_a_validation_error() =>
        Read("""{ "Nationality": { "Code": "FR", "Citizen": "Jane" } }""").ToCommand(_entryId, _referenceId, 1, _key)
            .Error.Code.ShouldBe("party.reference-field-not-supported");

    private static UpdatePartyReferenceRequest Read(string json) =>
        JsonSerializer.Deserialize<UpdatePartyReferenceRequest>(json, JsonSerializerOptions.Web).ShouldNotBeNull();
}
