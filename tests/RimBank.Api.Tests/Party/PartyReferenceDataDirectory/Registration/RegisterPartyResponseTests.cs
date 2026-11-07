using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

public sealed class RegisterPartyResponseTests
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private static readonly RegisterPartyAcknowledged _acknowledged = new(
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7()),
        1,
        PartyType.Organisation,
        _openDate,
        DemographicsId.From(Guid.CreateVersion7()),
        PartyReferenceId.From(Guid.CreateVersion7()),
        BankRelationsId.From(Guid.CreateVersion7()));

    [Fact]
    public void The_response_is_the_acknowledgement_in_BIAN_terms()
    {
        var response = RegisterPartyResponse.From(_acknowledged);

        response.ShouldBe(new RegisterPartyResponse(
            _acknowledged.EntryId.Value,
            "Organisation",
            "OpenDate",
            _openDate,
            _acknowledged.DemographicsId.Value,
            _acknowledged.PartyReferenceId.Value,
            _acknowledged.BankRelationsId.Value));
    }

    [Fact]
    public void The_response_uses_BIAN_property_names_whatever_the_naming_policy()
    {
        var json = JsonSerializer.Serialize(RegisterPartyResponse.From(_acknowledged), JsonSerializerOptions.Web);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(
        [
            "PartyReferenceDataDirectoryEntryId",
            "PartyType",
            "DirectoryEntryDateType",
            "DirectoryEntryDate",
            "DemographicsId",
            "ReferenceId",
            "BankRelationsId",
        ]);
        document.RootElement.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ShouldBe(_acknowledged.EntryId.Value);
        document.RootElement.GetProperty("PartyType").GetString().ShouldBe("Organisation");
        document.RootElement.GetProperty("DirectoryEntryDate").GetDateTimeOffset().ShouldBe(_openDate);
    }

    [Fact]
    public void The_request_reads_BIAN_property_names_whatever_the_naming_policy()
    {
        var request = JsonSerializer.Deserialize<RegisterPartyRequest>("""{ "PartyType": "Person" }""", JsonSerializerOptions.Web)!;

        request.PartyType.ShouldBe("Person");
    }
}
