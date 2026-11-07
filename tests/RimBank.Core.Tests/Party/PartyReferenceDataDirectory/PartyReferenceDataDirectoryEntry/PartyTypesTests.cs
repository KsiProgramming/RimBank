namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Roadmap 1.5, moved to the core in 1.7: the BIAN party type names every adapter (HTTP, MCP) accepts, and the errors
// for a missing or unknown one (ADR 0024).
public sealed class PartyTypesTests
{
    [Theory]
    [InlineData("Person", PartyType.Person)]
    [InlineData("Organisation", PartyType.Organisation)]
    public void A_BIAN_name_is_its_party_type(string name, PartyType expected) =>
        PartyTypes.FromBianName(name).Value.ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_name_is_required(string? name)
    {
        var parsed = PartyTypes.FromBianName(name);

        parsed.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyTypeRequired);
        parsed.Error.Code.ShouldBe("party.party-type-required");
        parsed.Error.FieldErrors.Keys.ShouldBe(["PartyType"]);
    }

    [Theory]
    [InlineData("Alien")]
    [InlineData("person")]
    [InlineData("ORGANISATION")]
    [InlineData("0")]
    [InlineData(" Person")]
    public void Any_other_name_is_invalid(string name) =>
        PartyTypes.FromBianName(name).Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid);
}
