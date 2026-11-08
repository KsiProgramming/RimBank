namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// BIAN Party_Partyrelationshiptypevalues (roadmap 1.11, ADR 0033): the 7 enum values and the 2 that only BIAN's
// description lists.
public sealed class PartyRelationshipTypesTests
{
    [Fact]
    public void There_are_nine_types_named_as_bian_names_them() =>
        Enum.GetValues<PartyRelationshipType>().Select(type => type.ToBianName()).ShouldBe(
        [
            "PartyIsParentOfParty",
            "PartyIsSubsidiaryOfParty",
            "PartyIsCertifiedByParty",
            "PartyIsMarriedToParty",
            "PartyActsOnBehalfOfParty",
            "PartyIsContactPointForParty",
            "PartyIsTrustedByParty",
            "PartyIsThePowerOfAttorneyForParty",
            "PartyReceivesBenefitsFromAParty",
        ]);

    [Theory]
    [InlineData("PartyIsParentOfParty", PartyRelationshipType.PartyIsParentOfParty)]
    [InlineData("partyismarriedtoparty", PartyRelationshipType.PartyIsMarriedToParty)]
    [InlineData(" PARTYISTHEPOWEROFATTORNEYFORPARTY ", PartyRelationshipType.PartyIsThePowerOfAttorneyForParty)]
    [InlineData("PartyReceivesBenefitsFromAParty", PartyRelationshipType.PartyReceivesBenefitsFromAParty)]
    public void A_type_is_read_trimmed_and_without_regard_to_case(string text, PartyRelationshipType type) =>
        PartyRelationshipTypes.Read(text).Value.ShouldBe(type);

    [Theory]
    [InlineData("Parent")]
    [InlineData("0")]
    [InlineData("Party Is Parent Of Party")]
    public void Another_text_is_invalid_and_names_the_associate_type(string text)
    {
        var error = PartyRelationshipTypes.Read(text).Error;

        error.Code.ShouldBe("party.association-invalid");
        error.FieldErrors.Keys.ShouldBe(["AssociateType"]);
    }

    [Fact]
    public void An_undefined_type_has_no_bian_name() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ((PartyRelationshipType)99).ToBianName());
}
