using System.Text.Json;
using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Registration;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Registration.Tests;

public sealed class RegisterPartyRequestTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.From("register-1");

    [Theory]
    [InlineData("Person", PartyType.Person)]
    [InlineData("Organisation", PartyType.Organisation)]
    public void A_BIAN_party_type_becomes_the_command(string partyType, PartyType expected)
    {
        var command = new RegisterPartyRequest { PartyType = partyType }.ToCommand(_key);

        command.Value.ShouldBe(new RegisterParty(expected, _key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_party_type_is_required(string? partyType)
    {
        var command = new RegisterPartyRequest { PartyType = partyType }.ToCommand(_key);

        command.Error.ShouldBe(RegisterPartyRequestErrors.PartyTypeRequired);
        command.Error.Code.ShouldBe("party.party-type-required");
        command.Error.FieldErrors.Keys.ShouldBe(["PartyType"]);
    }

    [Theory]
    [InlineData("Alien")]
    [InlineData("person")]
    [InlineData("ORGANISATION")]
    [InlineData("0")]
    [InlineData(" Person")]
    public void An_unknown_party_type_is_invalid(string partyType)
    {
        var command = new RegisterPartyRequest { PartyType = partyType }.ToCommand(_key);

        command.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid);
    }

    [Fact]
    public void The_other_BIAN_fields_are_accepted_and_ignored()
    {
        var request = JsonSerializer.Deserialize<RegisterPartyRequest>(
            """
            {
              "PartyReference": { "Identification": "ignored", "ContactPoint": "ignored" },
              "DirectoryEntryDateType": "RefreshDate",
              "DirectoryEntryDate": "1999-01-01T00:00:00Z",
              "PartyType": "Organisation"
            }
            """)!;

        request.ToCommand(_key).Value.ShouldBe(new RegisterParty(PartyType.Organisation, _key));
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var request = JsonSerializer.Deserialize<RegisterPartyRequest>("""{ "PartyType": "Person", "Unknown": 42 }""")!;

        request.ToCommand(_key).Value.ShouldBe(new RegisterParty(PartyType.Person, _key));
    }
}
