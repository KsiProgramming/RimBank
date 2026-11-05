namespace RimBank.SharedKernel.Tests;

public sealed class ResultOfTTests
{
    private static readonly Error _notFound = Error.NotFound("party.party-not-found", "No party with this identifier.");
    private static readonly Error _conflict = Error.Conflict("party.version-conflict", "The party was changed by someone else.");

    [Fact]
    public void Success_holds_its_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Failure_holds_its_error()
    {
        var result = Result.Failure<int>(_notFound);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(_notFound);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_is_a_programmer_error() =>
        Should.Throw<InvalidOperationException>(() => Result.Failure<int>(_notFound).Value)
            .Message.ShouldContain("party.party-not-found");

    [Fact]
    public void Reading_the_error_of_a_success_is_a_programmer_error() =>
        Should.Throw<InvalidOperationException>(() => Result.Success(42).Error);

    [Fact]
    public void A_success_needs_a_value() =>
        Should.Throw<ArgumentNullException>(() => Result.Success<string>(null!));

    [Fact]
    public void A_failure_needs_an_error() =>
        Should.Throw<ArgumentNullException>(() => Result.Failure<int>(null!));

    [Fact]
    public void Values_and_errors_convert_implicitly()
    {
        Result<string> success = "party-1";
        Result<string> failure = _notFound;

        success.Value.ShouldBe("party-1");
        failure.Error.ShouldBe(_notFound);
    }

    [Fact]
    public void Match_takes_the_success_branch() =>
        Result.Success(2).Match(value => $"value {value}", error => error.Code).ShouldBe("value 2");

    [Fact]
    public void Match_takes_the_failure_branch() =>
        Result.Failure<int>(_notFound).Match(value => $"value {value}", error => error.Code).ShouldBe("party.party-not-found");

    [Fact]
    public void Map_transforms_a_success() =>
        Result.Success(2).Map(value => value * 10).Value.ShouldBe(20);

    [Fact]
    public void Map_passes_a_failure_through_without_calling_the_function()
    {
        var called = false;

        var result = Result.Failure<int>(_notFound).Map(value => { called = true; return value * 10; });

        called.ShouldBeFalse();
        result.Error.ShouldBe(_notFound);
    }

    [Fact]
    public void Map_with_identity_changes_nothing()
    {
        Result.Success(7).Map(value => value).Value.ShouldBe(7);
        Result.Failure<int>(_notFound).Map(value => value).Error.ShouldBe(_notFound);
    }

    [Fact]
    public void Map_composes()
    {
        static int AddOne(int value) => value + 1;
        static int Double(int value) => value * 2;

        Result.Success(3).Map(AddOne).Map(Double).Value
            .ShouldBe(Result.Success(3).Map(value => Double(AddOne(value))).Value);
    }

    [Fact]
    public void Bind_chains_a_success_into_the_next_result() =>
        Result.Success(2).Bind(value => Result.Success($"party-{value}")).Value.ShouldBe("party-2");

    [Fact]
    public void Bind_returns_the_failure_of_the_next_step() =>
        Result.Success(2).Bind(_ => Result.Failure<string>(_conflict)).Error.ShouldBe(_conflict);

    [Fact]
    public void Bind_passes_a_failure_through_without_calling_the_function()
    {
        var called = false;

        var result = Result.Failure<int>(_notFound).Bind(value => { called = true; return Result.Success(value); });

        called.ShouldBeFalse();
        result.Error.ShouldBe(_notFound);
    }

    [Fact]
    public void Bind_is_associative()
    {
        static Result<int> Half(int value) => value % 2 == 0 ? value / 2 : Error.Validation("shared.not-even", "The value is odd.");
        static Result<int> Decrement(int value) => value > 0 ? value - 1 : Error.Validation("shared.not-positive", "The value is not positive.");

        foreach (var start in new[] { 0, 1, 2, 4, 7 })
        {
            var left = Result.Success(start).Bind(Half).Bind(Decrement);
            var right = Result.Success(start).Bind(value => Half(value).Bind(Decrement));

            left.ToString().ShouldBe(right.ToString());
        }
    }

    [Fact]
    public void Bind_to_a_non_generic_result_keeps_the_outcome()
    {
        Result.Success(2).Bind(_ => Result.Success()).IsSuccess.ShouldBeTrue();
        Result.Success(2).Bind(_ => Result.Failure(_conflict)).Error.ShouldBe(_conflict);
        Result.Failure<int>(_notFound).Bind(_ => Result.Success()).Error.ShouldBe(_notFound);
    }

    [Fact]
    public void MapError_transforms_a_failure() =>
        Result.Failure<int>(_notFound).MapError(_ => _conflict).Error.ShouldBe(_conflict);

    [Fact]
    public void MapError_leaves_a_success_alone()
    {
        var called = false;

        var result = Result.Success(2).MapError(error => { called = true; return error; });

        called.ShouldBeFalse();
        result.Value.ShouldBe(2);
    }

    [Fact]
    public void Tap_runs_an_action_on_success_and_returns_the_same_result()
    {
        var seen = 0;
        var original = Result.Success(5);

        var result = original.Tap(value => seen = value);

        seen.ShouldBe(5);
        result.ShouldBeSameAs(original);
    }

    [Fact]
    public void Tap_does_not_run_on_failure()
    {
        var called = false;

        Result.Failure<int>(_notFound).Tap(_ => called = true);

        called.ShouldBeFalse();
    }

    [Fact]
    public void ToString_describes_the_outcome()
    {
        Result.Success(5).ToString().ShouldBe("Success(5)");
        Result.Failure<int>(_notFound).ToString().ShouldBe("Failure(NotFound party.party-not-found: No party with this identifier.)");
    }
}
