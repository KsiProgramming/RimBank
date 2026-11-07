using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.8: the Update response is the acknowledgement in BIAN terms, in the Register shape plus RefreshDate.
public sealed class UpdatePartyReferenceDataDirectoryEntryResponseTests
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _refreshDate = _openDate.AddDays(30);

    private static readonly UpdatePartyReferenceDataDirectoryEntryAcknowledged _acknowledged = new(
        PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7()),
        Version: 2,
        PartyType.Organisation,
        _openDate,
        _refreshDate,
        DemographicsId.From(Guid.CreateVersion7()),
        PartyReferenceId.From(Guid.CreateVersion7()),
        BankRelationsId.From(Guid.CreateVersion7()));

    [Fact]
    public void The_response_is_the_acknowledgement_in_BIAN_terms() =>
        UpdatePartyReferenceDataDirectoryEntryResponse.From(_acknowledged).ShouldBe(new UpdatePartyReferenceDataDirectoryEntryResponse(
            _acknowledged.EntryId.Value,
            "Organisation",
            "OpenDate",
            _openDate,
            _refreshDate,
            _acknowledged.DemographicsId.Value,
            _acknowledged.PartyReferenceId.Value,
            _acknowledged.BankRelationsId.Value));

    [Fact]
    public void The_response_uses_BIAN_property_names_and_leaves_the_version_to_the_ETag()
    {
        var json = JsonSerializer.Serialize(UpdatePartyReferenceDataDirectoryEntryResponse.From(_acknowledged), JsonSerializerOptions.Web);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(
        [
            "PartyReferenceDataDirectoryEntryId",
            "PartyType",
            "DirectoryEntryDateType",
            "DirectoryEntryDate",
            "RefreshDate",
            "DemographicsId",
            "ReferenceId",
            "BankRelationsId",
        ]);
        document.RootElement.GetProperty("RefreshDate").GetDateTimeOffset().ShouldBe(_refreshDate);
    }
}
