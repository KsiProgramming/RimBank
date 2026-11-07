using System.Text.Json;
using RimBank.Party.PartyReferenceDataDirectory.BankRelations;
using RimBank.Party.PartyReferenceDataDirectory.Demographics;
using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Reference;
using RimBank.Party.PartyReferenceDataDirectory.Retrieval;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Retrieval.Tests;

public sealed class RetrievePartyReferenceDataDirectoryEntryResponseTests
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private static readonly PartyReferenceDataDirectoryEntryView _view = new(
        Guid.CreateVersion7(),
        Version: 4,
        PartyType.Person,
        _openDate,
        DemographicsId.From(Guid.CreateVersion7()),
        PartyReferenceId.From(Guid.CreateVersion7()),
        BankRelationsId.From(Guid.CreateVersion7()));

    [Fact]
    public void The_response_is_the_view_in_BIAN_terms()
    {
        var response = RetrievePartyReferenceDataDirectoryEntryResponse.From(_view);

        response.ShouldBe(new RetrievePartyReferenceDataDirectoryEntryResponse(
            _view.Id,
            "Person",
            "OpenDate",
            _openDate,
            _view.DemographicsId.Value,
            _view.PartyReferenceId.Value,
            _view.BankRelationsId.Value));
    }

    [Fact]
    public void The_response_uses_BIAN_property_names_and_leaves_the_version_to_the_ETag()
    {
        var json = JsonSerializer.Serialize(RetrievePartyReferenceDataDirectoryEntryResponse.From(_view), JsonSerializerOptions.Web);

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
        document.RootElement.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ShouldBe(_view.Id);
        document.RootElement.GetProperty("DirectoryEntryDate").GetDateTimeOffset().ShouldBe(_openDate);
    }
}
