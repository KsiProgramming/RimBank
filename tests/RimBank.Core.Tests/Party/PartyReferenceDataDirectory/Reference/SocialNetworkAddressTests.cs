namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The social network contact point of the Reference BQ (roadmap 1.19, ADR 0031): an absolute https URL.
public sealed class SocialNetworkAddressTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/in/jdupont")]
    [InlineData(" https://mastodon.social/@jdupont ")]
    public void An_https_url_is_trimmed_and_kept_as_written(string text) =>
        SocialNetworkAddress.TryCreate(text).Value.Value.ShouldBe(text.Trim());

    [Fact]
    public void Two_thousand_and_forty_eight_characters_are_accepted() =>
        SocialNetworkAddress.TryCreate("https://example.com/" + new string('a', 2028)).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("http://www.linkedin.com/in/jdupont")]
    [InlineData("www.linkedin.com/in/jdupont")]
    [InlineData("/in/jdupont")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://")]
    [InlineData("https://exa mple.com/jdupont")]
    [InlineData("")]
    public void Anything_but_an_absolute_https_url_is_invalid(string text) =>
        SocialNetworkAddress.TryCreate(text).Error.Message
            .ShouldBe("The social network contact must be an absolute https URL, for example https://www.linkedin.com/in/name.");

    [Fact]
    public void Two_thousand_and_forty_nine_characters_are_invalid() =>
        SocialNetworkAddress.TryCreate("https://example.com/" + new string('a', 2029)).Error.Message
            .ShouldBe("The social network contact must be at most 2048 characters.");
}
