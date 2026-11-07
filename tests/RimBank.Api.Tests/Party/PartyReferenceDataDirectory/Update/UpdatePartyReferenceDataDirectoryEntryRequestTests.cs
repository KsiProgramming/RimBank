using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.8: the BIAN Update body becomes the command, with the route's entry and the If-Match version.
public sealed class UpdatePartyReferenceDataDirectoryEntryRequestTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.From("update-1");
    private static readonly PartyReferenceDataDirectoryEntryId _entryId = PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7());

    [Theory]
    [InlineData("Person", PartyType.Person)]
    [InlineData("Organisation", PartyType.Organisation)]
    public void A_BIAN_party_type_becomes_the_command(string partyType, PartyType expected) =>
        new UpdatePartyReferenceDataDirectoryEntryRequest { PartyType = partyType }.ToCommand(_entryId, 3, _key)
            .Value.ShouldBe(new UpdatePartyReferenceDataDirectoryEntry(_entryId, 3, expected, _key));

    [Fact]
    public void No_party_type_keeps_the_current_one() =>
        new UpdatePartyReferenceDataDirectoryEntryRequest().ToCommand(_entryId, 1, _key)
            .Value.ShouldBe(new UpdatePartyReferenceDataDirectoryEntry(_entryId, 1, null, _key));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("person")]
    [InlineData("Alien")]
    public void A_blank_or_unknown_party_type_is_invalid(string partyType)
    {
        var command = new UpdatePartyReferenceDataDirectoryEntryRequest { PartyType = partyType }.ToCommand(_entryId, 1, _key);

        command.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid);
        command.Error.FieldErrors.Keys.ShouldBe(["PartyType"]);
    }

    [Fact]
    public void The_other_BIAN_fields_are_accepted_and_ignored()
    {
        var request = JsonSerializer.Deserialize<UpdatePartyReferenceDataDirectoryEntryRequest>(
            """
            {
              "PartyReference": { "PartyName": "Ada" },
              "DirectoryEntryDateType": { "Directoryentrydatetypevalues": "RefreshDate" },
              "DirectoryEntryDate": "2020-01-01T00:00:00Z",
              "PartyType": "Organisation"
            }
            """,
            JsonSerializerOptions.Web).ShouldNotBeNull();

        request.ToCommand(_entryId, 2, _key).Value.ShouldBe(new UpdatePartyReferenceDataDirectoryEntry(_entryId, 2, PartyType.Organisation, _key));
    }
}
