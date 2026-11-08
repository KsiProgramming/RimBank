using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The email address contact point of the Reference BQ (roadmap 1.19, ADR 0031): trimmed, a syntax check only.
public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("jeanne@example.fr")]
    [InlineData(" jeanne@example.fr ")]
    public void An_email_address_is_trimmed_and_kept_as_written(string text) =>
        EmailAddress.TryCreate(text).Value.Value.ShouldBe("jeanne@example.fr");

    [Theory]
    [InlineData("Jeanne.Dupont+bank@Mail.Example.FR")]
    [InlineData("j@x.io")]
    public void Common_email_addresses_are_accepted(string text) =>
        EmailAddress.TryCreate(text).Value.Value.ShouldBe(text);

    [Fact]
    public void Two_hundred_and_fifty_six_characters_are_accepted() =>
        EmailAddress.TryCreate(new string('a', 245) + "@example.fr").IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("jeanne.example.fr")] // no @
    [InlineData("jeanne@dupont@example.fr")] // two @
    [InlineData("@example.fr")] // empty local part
    [InlineData("jeanne@")] // empty domain
    [InlineData("jeanne@localhost")] // a domain without a dot
    [InlineData("jeanne@example..fr")] // an empty domain label
    [InlineData("jeanne@.example.fr")]
    [InlineData("jeanne@example.fr.")]
    [InlineData("jeanne dupont@example.fr")] // inner whitespace
    [InlineData("Jeanne <jeanne@example.fr>")] // a display name
    [InlineData("")]
    public void A_malformed_email_address_is_invalid(string text) =>
        EmailAddress.TryCreate(text).Error.ShouldSatisfyAllConditions(
            error => error.Code.ShouldBe("party.reference-invalid"),
            error => error.Kind.ShouldBe(ErrorKind.Validation));

    [Fact]
    public void Two_hundred_and_fifty_seven_characters_are_invalid() =>
        EmailAddress.TryCreate(new string('a', 246) + "@example.fr").Error.Message
            .ShouldBe("The email address must be at most 256 characters.");

    [Fact]
    public void It_is_a_plain_json_string() =>
        JsonSerializer.Serialize(EmailAddress.TryCreate("jeanne@example.fr").Value).ShouldBe("\"jeanne@example.fr\"");
}
