using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The declared political exposure of the Reference BQ (roadmap 1.20, ADR 0032): the BIAN type and a Max350Text description.
public sealed class PoliticalExposureTests
{
    [Theory]
    [InlineData("Politicalexposureforeign", PoliticalExposureType.Foreign)]
    [InlineData("POLITICALEXPOSUREDOMESTIC", PoliticalExposureType.Domestic)]
    [InlineData(" nopoliticalexposure ", PoliticalExposureType.NoExposure)]
    public void A_type_is_read_without_regard_to_case(string text, PoliticalExposureType type) =>
        PoliticalExposureTypes.Read(text).Value.ShouldBe(type);

    [Theory]
    [InlineData(PoliticalExposureType.Foreign, "Politicalexposureforeign", true)]
    [InlineData(PoliticalExposureType.Domestic, "Politicalexposuredomestic", true)]
    [InlineData(PoliticalExposureType.NoExposure, "Nopoliticalexposure", false)]
    public void A_type_has_its_bian_name_and_says_whether_it_is_an_exposure(PoliticalExposureType type, string name, bool exposed)
    {
        type.ToBianName().ShouldBe(name);
        type.IsExposed().ShouldBe(exposed);
    }

    [Theory]
    [InlineData("Foreign")]
    [InlineData("PEP")]
    public void Another_type_is_invalid(string text) =>
        PoliticalExposureTypes.Read(text).Error.Code.ShouldBe("party.reference-invalid");

    [Fact]
    public void A_description_is_trimmed() =>
        PoliticalExposureDescription.TryCreate("  Member of a regional council  ").Value.Value
            .ShouldBe("Member of a regional council");

    [Fact]
    public void A_description_of_350_characters_is_accepted() =>
        PoliticalExposureDescription.TryCreate(new string('d', 350)).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(351)]
    [InlineData(0)]
    public void A_description_that_is_too_long_or_blank_is_invalid(int length) =>
        PoliticalExposureDescription.TryCreate(new string('d', length)).Error.Code.ShouldBe("party.reference-invalid");

    [Fact]
    public void A_description_is_a_plain_json_string() =>
        JsonSerializer.Serialize(PoliticalExposureDescription.TryCreate("Mayor").Value).ShouldBe("\"Mayor\"");
}
