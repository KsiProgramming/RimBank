namespace RimBank.SharedKernel.Tests;

public sealed class TypedIdParsingTests
{
    private const string Text = "019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f";

    private static readonly SampleId _id = SampleId.From(Guid.ParseExact(Text, "D"));

    public static TheoryData<string?> InvalidTexts => new()
    {
        (string?)null,
        "",
        "   ",
        "not-an-identifier",
        "00000000-0000-0000-0000-000000000000",          // the nil GUID
        "3f2504e0-4f89-41d3-9a0c-0305e82c3301",          // version 4
        "019a5a3e4f2b7c3d8e9f0a1b2c3d4e5f",              // N format: only D is accepted
        "{019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f}",        // B format
        " 019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f",         // surrounding white space
        "019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f\n",
    };

    [Fact]
    public void Parse_accepts_upper_case_hex() =>
        Parse<SampleId>(Text.ToUpperInvariant()).ShouldBe(_id);

    [Fact]
    public void Span_parse_round_trips_the_text_form() =>
        SpanParse<SampleId>(_id.ToString().AsSpan()).ShouldBe(_id);

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void TryParse_rejects_invalid_text(string? text)
    {
        TryParse<SampleId>(text, out _).ShouldBeFalse();
        SpanTryParse<SampleId>(text.AsSpan(), out _).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void TryCreate_rejects_invalid_text_with_a_validation_error(string? text)
    {
        var result = SampleId.TryCreate(text);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe(TypedId.InvalidIdentifierCode);
    }

    [Fact]
    public void TryCreate_does_not_echo_the_untrusted_input() =>
        SampleId.TryCreate("<script>").Error.Message.ShouldNotContain("<script>");

    [Fact]
    public void TryCreate_accepts_valid_text() =>
        SampleId.TryCreate(Text).Value.ShouldBe(_id);

    [Fact]
    public void Parse_throws_FormatException_for_invalid_text() =>
        Should.Throw<FormatException>(() => Parse<SampleId>("not-an-identifier"));

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("D")]
    [InlineData("d")]
    public void Formatting_supports_the_D_format(string? format) =>
        ((IFormattable)_id).ToString(format, null).ShouldBe(Text);

    [Theory]
    [InlineData("N")]
    [InlineData("B")]
    [InlineData("X")]
    public void Formatting_rejects_other_formats(string format) =>
        Should.Throw<FormatException>(() => ((IFormattable)_id).ToString(format, null));

    [Fact]
    public void TryFormat_writes_the_same_text()
    {
        Span<char> buffer = stackalloc char[64];

        ((ISpanFormattable)_id).TryFormat(buffer, out var written, default, null).ShouldBeTrue();

        buffer[..written].ToString().ShouldBe(Text);
    }

    [Fact]
    public void TryFormat_returns_false_when_the_buffer_is_too_small()
    {
        Span<char> buffer = stackalloc char[10];

        ((ISpanFormattable)_id).TryFormat(buffer, out var written, default, null).ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void Interpolation_uses_the_text_form() =>
        $"{_id}".ShouldBe(Text);

    private static T Parse<T>(string text)
        where T : IParsable<T> => T.Parse(text, null);

    private static T SpanParse<T>(ReadOnlySpan<char> text)
        where T : ISpanParsable<T> => T.Parse(text, null);

    private static bool TryParse<T>(string? text, out T? result)
        where T : IParsable<T> => T.TryParse(text, null, out result);

    private static bool SpanTryParse<T>(ReadOnlySpan<char> text, out T? result)
        where T : ISpanParsable<T> => T.TryParse(text, null, out result);
}
