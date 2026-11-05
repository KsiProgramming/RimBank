using RimBank.SharedKernel;

namespace RimBank.Problems.Tests;

// The ADR 0006 kind → status table, applied in one place (ADR 0015).
public sealed class ErrorStatusTests
{
    public static TheoryData<ErrorKind, int> StatusByKind => new()
    {
        { ErrorKind.Validation, 400 },
        { ErrorKind.Unauthorized, 401 },
        { ErrorKind.Forbidden, 403 },
        { ErrorKind.NotFound, 404 },
        { ErrorKind.Conflict, 409 },
        { ErrorKind.BusinessRule, 422 },
        { ErrorKind.Unexpected, 500 },
    };

    [Theory]
    [MemberData(nameof(StatusByKind))]
    public void Each_kind_has_its_ADR_0006_status(ErrorKind kind, int status)
    {
        ErrorStatus.For(kind).ShouldBe(status);
    }

    [Fact]
    public void Every_kind_is_in_the_table()
    {
        var tested = StatusByKind.Select(row => (ErrorKind)row.Data.Item1).ToList();

        Enum.GetValues<ErrorKind>().ShouldBe(tested, ignoreOrder: true);
    }

    [Fact]
    public void An_undefined_kind_is_a_programmer_error()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ErrorStatus.For((ErrorKind)99));
    }
}
