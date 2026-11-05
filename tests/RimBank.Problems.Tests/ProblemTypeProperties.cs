using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using RimBank.SharedKernel;

namespace RimBank.Problems.Tests;

// Any code that follows the ADR 0006 convention gives a well-formed problem type URN (ADR 0015).
[Properties(Arbitrary = [typeof(ErrorCodeArbitraries)])]
public sealed class ProblemTypeProperties
{
    [Property]
    public bool A_valid_code_gives_an_absolute_urn_with_the_same_segments(ErrorCode code)
    {
        var type = ProblemType.For(Error.NotFound(code.Value, "Not here."));
        var segments = code.Value.Split('.');

        return Uri.TryCreate(type, UriKind.Absolute, out var uri)
            && uri.Scheme == "urn"
            && type == $"urn:rimbank:problem:{segments[0]}:{segments[1]}";
    }
}

/// <summary>A code of the form <c>&lt;context&gt;.&lt;error-name&gt;</c>, lower-case kebab-case.</summary>
public sealed record ErrorCode(string Value);

public static class ErrorCodeArbitraries
{
    public static Arbitrary<ErrorCode> ErrorCodes()
    {
        var letter = Gen.Elements("abcdefghijklmnopqrstuvwxyz".ToCharArray());
        var letterOrDigit = Gen.Elements("abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray());
        var word = Gen.ArrayOf(letterOrDigit).Select(chars => new string(chars));
        var segment =
            from first in letter
            from rest in word
            from parts in Gen.ArrayOf(Gen.NonEmptyListOf(letterOrDigit).Select(chars => "-" + string.Concat(chars)))
            select first + rest + string.Concat(parts);

        return (from context in segment from name in segment select new ErrorCode($"{context}.{name}")).ToArbitrary();
    }
}
