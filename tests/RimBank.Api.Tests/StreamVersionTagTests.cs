using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using RimBank.SharedKernel;

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

    // Roadmap 1.8: a command reads the version it expects from If-Match (ADR 0027).
    [Theory]
    [InlineData("\"1\"", 1)]
    [InlineData("\"2\"", 2)]
    [InlineData(" \"42\" ", 42)]
    public void If_Match_with_one_strong_version_tag_is_the_expected_version(string ifMatch, long expected) =>
        StreamVersionTag.ReadIfMatch(IfMatch(ifMatch)).Value.ShouldBe(expected);

    [Fact]
    public void A_missing_If_Match_is_a_required_precondition()
    {
        var read = StreamVersionTag.ReadIfMatch(IfMatch(null));

        read.Error.ShouldBe(StreamVersionTag.IfMatchRequired);
        read.Error.Kind.ShouldBe(ErrorKind.PreconditionRequired);
        read.Error.Code.ShouldBe("shared.if-match-required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("W/\"2\"")]
    [InlineData("*")]
    [InlineData("\"1\", \"2\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"0\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"+2\"")]
    [InlineData("\" 2\"")]
    [InlineData("2")]
    [InlineData("\"99999999999999999999\"")]
    [InlineData("not a tag")]
    public void Any_other_If_Match_is_invalid(string ifMatch)
    {
        var read = StreamVersionTag.ReadIfMatch(IfMatch(ifMatch));

        read.Error.ShouldBe(StreamVersionTag.IfMatchInvalid);
        read.Error.Kind.ShouldBe(ErrorKind.Validation);
        read.Error.Code.ShouldBe("shared.if-match-invalid");
        read.Error.FieldErrors.Keys.ShouldBe([HeaderNames.IfMatch]);
    }

    [Fact]
    public void Two_If_Match_headers_are_invalid()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[HeaderNames.IfMatch] = new Microsoft.Extensions.Primitives.StringValues(["\"1\"", "\"1\""]);

        StreamVersionTag.ReadIfMatch(http.Request).Error.ShouldBe(StreamVersionTag.IfMatchInvalid);
    }

    private static HttpRequest IfMatch(string? ifMatch)
    {
        var http = new DefaultHttpContext();
        if (ifMatch is not null)
        {
            http.Request.Headers[HeaderNames.IfMatch] = ifMatch;
        }

        return http.Request;
    }

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
