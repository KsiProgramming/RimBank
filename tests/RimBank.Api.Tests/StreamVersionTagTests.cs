using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace RimBank.Api.Tests;

// Roadmap 1.7: a record's stream version travels as a strong ETag, and a conditional GET whose If-None-Match already
// names it is not modified (specs/tech-stack.md §5, RFC 9110 §8.8.3 and §13.1.2).
public sealed class StreamVersionTagTests
{
    [Theory]
    [InlineData(1, "\"1\"")]
    [InlineData(42, "\"42\"")]
    public void A_version_is_a_strong_quoted_entity_tag(long version, string expected) =>
        StreamVersionTag.Format(version).ShouldBe(expected);

    [Fact]
    public void A_version_below_one_is_not_a_stream_version() =>
        Should.Throw<ArgumentOutOfRangeException>(() => StreamVersionTag.Format(0));

    [Fact]
    public void The_version_is_set_as_the_ETag_header()
    {
        var http = new DefaultHttpContext();

        StreamVersionTag.SetOn(http.Response, 3);

        http.Response.Headers.ETag.ToString().ShouldBe("\"3\"");
    }

    [Theory]
    [InlineData("\"3\"")]
    [InlineData("W/\"3\"")]
    [InlineData("\"1\", \"3\"")]
    [InlineData("*")]
    public void If_None_Match_naming_the_current_version_means_not_modified(string ifNoneMatch) =>
        StreamVersionTag.IsNotModified(Request(ifNoneMatch), 3).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("\"2\"")]
    [InlineData("\"1\", W/\"2\"")]
    [InlineData("3")]
    [InlineData("not a tag")]
    public void Any_other_If_None_Match_means_modified(string? ifNoneMatch) =>
        StreamVersionTag.IsNotModified(Request(ifNoneMatch), 3).ShouldBeFalse();

    private static HttpRequest Request(string? ifNoneMatch)
    {
        var http = new DefaultHttpContext();
        if (ifNoneMatch is not null)
        {
            http.Request.Headers[HeaderNames.IfNoneMatch] = ifNoneMatch;
        }

        return http.Request;
    }
}
