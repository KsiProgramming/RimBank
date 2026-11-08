using System.Text.Json;
using System.Text.Json.Nodes;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.10: the Reference response, built from the Update's acknowledgement or from the entry's view, in BIAN's
// property names, leaving out what is not known (ADR 0029).
public sealed class PartyReferenceResponseTests
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly PartyReferenceDataDirectoryEntryId _entryId = PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(_openDate));
    private static readonly PartyReferenceId _referenceId = PartyReferenceId.From(Guid.CreateVersion7(_openDate));

    private static readonly PartyReferenceDetails _details = new(new PartyIdentifiers(
        TaxIdentificationNumber.TryCreate("FR12345678901").Value,
        null,
        LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value));

    [Fact]
    public void The_acknowledgement_becomes_the_bian_shape_without_unknown_identifiers()
    {
        var response = PartyReferenceResponse.From(new UpdatePartyReferenceAcknowledged(_entryId, _referenceId, 2, _details));

        var json = JsonNode.Parse(JsonSerializer.Serialize(response)).ShouldNotBeNull().AsObject();

        json["PartyReferenceDataDirectoryEntryId"]!.GetValue<Guid>().ShouldBe(_entryId.Value);
        json["ReferenceId"]!.GetValue<Guid>().ShouldBe(_referenceId.Value);
        json["GovernmentIssuedIdentityReference"]!.ToJsonString().ShouldBe("""{"TaxIdentificationNumber":"FR12345678901"}""");
        json["PartyLegalEntityReference"]!.ToJsonString().ShouldBe("""{"Identification":"5493001KJTIIGC8Y1R12"}""");
    }

    [Fact]
    public void The_view_s_reference_section_becomes_the_same_response()
    {
        var view = new PartyReferenceDataDirectoryEntryView(
            _entryId.Value, 2, PartyType.Organisation, _openDate,
            DemographicsId.From(Guid.CreateVersion7(_openDate)), _referenceId, BankRelationsId.From(Guid.CreateVersion7(_openDate)),
            Reference: _details);

        PartyReferenceResponse.From(view)
            .ShouldBe(PartyReferenceResponse.From(new UpdatePartyReferenceAcknowledged(_entryId, _referenceId, 2, _details)));
    }

    [Fact]
    public void A_view_with_no_reference_section_has_only_the_ids()
    {
        var view = new PartyReferenceDataDirectoryEntryView(
            _entryId.Value, 1, PartyType.Person, _openDate,
            DemographicsId.From(Guid.CreateVersion7(_openDate)), _referenceId, BankRelationsId.From(Guid.CreateVersion7(_openDate)));

        JsonSerializer.Serialize(PartyReferenceResponse.From(view))
            .ShouldBe($$"""{"PartyReferenceDataDirectoryEntryId":"{{_entryId.Value}}","ReferenceId":"{{_referenceId.Value}}"}""");
    }
}
