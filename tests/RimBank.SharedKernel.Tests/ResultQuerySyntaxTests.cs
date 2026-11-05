namespace RimBank.SharedKernel.Tests;

public sealed class ResultQuerySyntaxTests
{
    private static readonly Error _notFound = Error.NotFound("party.party-not-found", "No party with this identifier.");
    private static readonly Error _conflict = Error.Conflict("party.version-conflict", "The party was changed by someone else.");

    [Fact]
    public void Select_maps_a_success()
    {
        var result = from value in Result.Success(2) select value * 10;

        result.Value.ShouldBe(20);
    }

    [Fact]
    public void Query_syntax_combines_successes()
    {
        var result =
            from first in Result.Success(2)
            from second in Result.Success(3)
            from third in Result.Success(4)
            select first + second + third;

        result.Value.ShouldBe(9);
    }

    [Fact]
    public void Query_syntax_returns_the_first_failure_and_stops()
    {
        var thirdCalled = false;

        var result =
            from first in Result.Success(2)
            from second in Result.Failure<int>(_notFound)
            from third in Track(() => thirdCalled = true, Result.Failure<int>(_conflict))
            select first + second + third;

        result.Error.ShouldBe(_notFound);
        thirdCalled.ShouldBeFalse();
    }

    private static Result<int> Track(Action onCall, Result<int> result)
    {
        onCall();
        return result;
    }
}
