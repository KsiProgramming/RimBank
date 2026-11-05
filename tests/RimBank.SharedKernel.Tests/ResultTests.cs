namespace RimBank.SharedKernel.Tests;

public sealed class ResultTests
{
    private static readonly Error _conflict = Error.Conflict("party.version-conflict", "The party was changed by someone else.");
    private static readonly Error _forbidden = Error.Forbidden("party.not-allowed", "You may not change this party.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_holds_its_error()
    {
        var result = Result.Failure(_conflict);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(_conflict);
    }

    [Fact]
    public void A_failure_needs_an_error() =>
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));

    [Fact]
    public void Errors_convert_implicitly()
    {
        Result result = _conflict;

        result.Error.ShouldBe(_conflict);
    }

    [Fact]
    public void Match_takes_the_right_branch()
    {
        Result.Success().Match(() => "acknowledged", error => error.Code).ShouldBe("acknowledged");
        Result.Failure(_conflict).Match(() => "acknowledged", error => error.Code).ShouldBe("party.version-conflict");
    }

    [Fact]
    public void Map_produces_a_value_on_success_only()
    {
        Result.Success().Map(() => 3).Value.ShouldBe(3);
        Result.Failure(_conflict).Map(() => 3).Error.ShouldBe(_conflict);
    }

    [Fact]
    public void Bind_chains_on_success_only()
    {
        var called = false;

        Result.Success().Bind(() => Result.Failure(_forbidden)).Error.ShouldBe(_forbidden);
        Result.Success().Bind(() => Result.Success("ack")).Value.ShouldBe("ack");
        Result.Failure(_conflict).Bind(() => { called = true; return Result.Success(); }).Error.ShouldBe(_conflict);
        Result.Failure(_conflict).Bind(() => { called = true; return Result.Success(1); }).Error.ShouldBe(_conflict);

        called.ShouldBeFalse();
    }

    [Fact]
    public void MapError_transforms_a_failure_only()
    {
        Result.Failure(_conflict).MapError(_ => _forbidden).Error.ShouldBe(_forbidden);
        Result.Success().MapError(_ => _forbidden).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Tap_runs_on_success_only()
    {
        var calls = 0;

        Result.Success().Tap(() => calls++);
        Result.Failure(_conflict).Tap(() => calls++);

        calls.ShouldBe(1);
    }

    [Fact]
    public void ToString_describes_the_outcome()
    {
        Result.Success().ToString().ShouldBe("Success");
        Result.Failure(_conflict).ToString().ShouldBe("Failure(Conflict party.version-conflict: The party was changed by someone else.)");
    }
}
