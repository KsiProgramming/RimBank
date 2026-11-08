using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The mobile number contact point of the Reference BQ (roadmap 1.19, ADR 0031): E.164, normalised.
public sealed class MobileNumberTests
{
    [Theory]
    [InlineData("+33612345678")]
    [InlineData(" +33 6 12 34 56 78 ")]
    [InlineData("+33-612345678")] // the ISO 20022 PhoneNumber form
    [InlineData("+33.6.12.34.56.78")]
    public void A_mobile_number_is_normalised_to_e164(string text) =>
        MobileNumber.TryCreate(text).Value.Value.ShouldBe("+33612345678");

    [Theory]
    [InlineData("+6834002")] // 7 digits: Niue
    [InlineData("+123456789012345")] // 15 digits: the E.164 maximum
    public void The_e164_lengths_are_accepted(string text) =>
        MobileNumber.TryCreate(text).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("0612345678")] // no country code
    [InlineData("+0612345678")] // a country code cannot start with 0
    [InlineData("+33 6 12 AB 56 78")]
    [InlineData("+33 (0)6 12 34 56 78")] // stripping the brackets would keep the trunk 0: a wrong number
    [InlineData("+1234567890123456")] // 16 digits
    [InlineData("+683400")] // 6 digits
    [InlineData("++33612345678")]
    [InlineData("")]
    public void A_number_that_is_not_e164_is_invalid(string text) =>
        MobileNumber.TryCreate(text).Error.ShouldSatisfyAllConditions(
            error => error.Code.ShouldBe("party.reference-invalid"),
            error => error.Message.ShouldBe(
                "The mobile number must be an international E.164 number, for example +33612345678."));

    [Fact]
    public void It_is_a_plain_json_string() =>
        JsonSerializer.Deserialize<MobileNumber>("\"+33612345678\"").ShouldBe(MobileNumber.TryCreate("+33612345678").Value);
}
