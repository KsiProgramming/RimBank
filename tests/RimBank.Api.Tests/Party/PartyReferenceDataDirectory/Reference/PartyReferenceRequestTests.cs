using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.10: the BIAN Reference Update body becomes its command, with the route's ids and the If-Match version; the
// Reference fields RimBank does not record yet are found and refused (ADR 0024, ADR 0029).
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
    public void An_empty_body_changes_nothing() =>
        Read("{}").ToCommand(_entryId, _referenceId, 1, _key).Value.Changes.ShouldBe(PartyReferenceChanges.None);

    [Fact]
    public void Staged_fields_and_unrecorded_parts_are_unsupported_whatever_their_case()
    {
        var request = Read(
            """
            {
              "dateofbirth": "1990-01-01",
              "CompanyOfficerRole": { "PartyRole": "Director" },
              "GovernmentIssuedIdentityReference": { "OtherIdentification": "X1", "Declaration": null },
              "PartyLegalEntityReference": { "ContactPoint": "x" }
            }
            """);

        request.UnsupportedFields().ShouldBe(
            [
                "DateofBirth",
                "CompanyOfficerRole",
                "GovernmentIssuedIdentityReference.OtherIdentification",
                "PartyLegalEntityReference.ContactPoint",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Fields_sent_as_null_and_properties_that_are_not_BIAN_s_are_not_unsupported() =>
        Read("""{ "DateofBirth": null, "NotABianField": "x" }""").UnsupportedFields().ShouldBeEmpty();

    [Fact]
    public void An_unsupported_field_makes_the_command_a_validation_error() =>
        Read("""{ "Nationality": { "Code": "FR" } }""").ToCommand(_entryId, _referenceId, 1, _key)
            .Error.Code.ShouldBe("party.reference-field-not-supported");

    private static UpdatePartyReferenceRequest Read(string json) =>
        JsonSerializer.Deserialize<UpdatePartyReferenceRequest>(json, JsonSerializerOptions.Web).ShouldNotBeNull();
}
