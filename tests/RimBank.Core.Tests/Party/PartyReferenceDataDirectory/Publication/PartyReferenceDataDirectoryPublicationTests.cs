using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using Published = RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Roadmap 1.13: the Control Record's domain events mapped to the published language (ADR 0024, ADR 0036). The mapping
// reads the decided event only, never a view, and carries the state a consumer needs.
public sealed class PartyReferenceDataDirectoryPublicationTests
{
    private static readonly Guid _eventId = Guid.CreateVersion7(OpenDate);

    [Theory]
    [InlineData(PartyType.Person, Published.PartyTypes.Person)]
    [InlineData(PartyType.Organisation, Published.PartyTypes.Organisation)]
    public void A_registration_is_published_as_the_entry_created_with_its_open_date(PartyType partyType, string publishedPartyType)
    {
        var created = PartyReferenceDataDirectoryPublication.Created(Registered(partyType), 1, _eventId);

        created.ShouldBe(new Published.PartyReferenceDataDirectoryEntryCreated(
            _eventId,
            OpenDate,
            EntryId.Value,
            1,
            publishedPartyType,
            Published.DirectoryEntryDateTypes.OpenDate,
            OpenDate));
    }

    [Theory]
    [InlineData(PartyType.Person, Published.PartyTypes.Person)]
    [InlineData(PartyType.Organisation, Published.PartyTypes.Organisation)]
    public void An_update_is_published_as_the_entry_updated_with_its_refresh_date(PartyType partyType, string publishedPartyType)
    {
        var updated = PartyReferenceDataDirectoryPublication.Updated(Updated(partyType), 7, _eventId);

        updated.ShouldBe(new Published.PartyReferenceDataDirectoryEntryUpdated(
            _eventId,
            RefreshDate,
            EntryId.Value,
            7,
            publishedPartyType,
            Published.DirectoryEntryDateTypes.RefreshDate,
            RefreshDate));
    }

    [Fact]
    public void Every_party_type_has_a_published_value()
    {
        Enum.GetValues<PartyType>()
            .Select(partyType => PartyReferenceDataDirectoryPublication.Created(Registered(partyType), 1, _eventId).PartyType)
            .ShouldBe([Published.PartyTypes.Person, Published.PartyTypes.Organisation]);
    }

    [Fact]
    public void A_value_outside_the_party_types_is_never_published()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PartyReferenceDataDirectoryPublication.Created(Registered((PartyType)99), 1, _eventId));
    }
}
