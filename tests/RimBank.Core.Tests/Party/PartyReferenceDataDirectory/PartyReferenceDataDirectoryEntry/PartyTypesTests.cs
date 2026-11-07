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

    // Roadmap 1.8: an Update may leave the party type out to keep the current one.
    [Fact]
    public void An_update_without_a_name_asks_for_no_party_type() =>
        PartyTypes.FromOptionalBianName(null).ShouldBeNull();

    [Theory]
    [InlineData("Person", PartyType.Person)]
    [InlineData("Organisation", PartyType.Organisation)]
    public void An_update_with_a_BIAN_name_asks_for_its_party_type(string name, PartyType expected) =>
        PartyTypes.FromOptionalBianName(name).ShouldNotBeNull().Value.ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("person")]
    [InlineData("Alien")]
    [InlineData("1")]
    public void An_update_with_a_blank_or_unknown_name_is_invalid(string name) =>
        PartyTypes.FromOptionalBianName(name).ShouldNotBeNull().Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid);
}
