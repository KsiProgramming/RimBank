namespace RimBank.SharedKernel.Tests;

public sealed class ResultAsyncTests
{
    private static readonly Error _notFound = Error.NotFound("party.party-not-found", "No party with this identifier.");
    private static readonly Error _conflict = Error.Conflict("party.version-conflict", "The party was changed by someone else.");

    private static Task<Result<int>> Loaded(int value) => Task.FromResult(Result.Success(value));

    private static Task<Result<int>> Missing() => Task.FromResult(Result.Failure<int>(_notFound));

    [Fact]
    public async Task MapAsync_with_an_async_function_transforms_a_success()
    {
        var result = await Result.Success(2).MapAsync(async value => { await Task.Yield(); return value * 10; });

        result.Value.ShouldBe(20);
    }

    [Fact]
    public async Task MapAsync_on_a_task_transforms_a_success()
    {
        (await Loaded(2).MapAsync(value => value * 10)).Value.ShouldBe(20);
        (await Loaded(2).MapAsync(value => Task.FromResult(value * 10))).Value.ShouldBe(20);
    }

    [Fact]
    public async Task MapAsync_does_not_call_the_function_on_failure()
    {
        var called = false;

        var fromResult = await Result.Failure<int>(_notFound).MapAsync(value => { called = true; return Task.FromResult(value); });
        var fromTask = await Missing().MapAsync(value => { called = true; return value; });
        var fromTaskAsync = await Missing().MapAsync(value => { called = true; return Task.FromResult(value); });

        called.ShouldBeFalse();
        fromResult.Error.ShouldBe(_notFound);
        fromTask.Error.ShouldBe(_notFound);
        fromTaskAsync.Error.ShouldBe(_notFound);
    }

    [Fact]
    public async Task BindAsync_chains_successes()
    {
        (await Result.Success(2).BindAsync(value => Loaded(value + 1))).Value.ShouldBe(3);
        (await Loaded(2).BindAsync(value => Result.Success(value + 1))).Value.ShouldBe(3);
        (await Loaded(2).BindAsync(value => Loaded(value + 1))).Value.ShouldBe(3);
    }

    [Fact]
    public async Task BindAsync_returns_the_failure_of_the_next_step() =>
        (await Loaded(2).BindAsync(_ => Task.FromResult(Result.Failure<int>(_conflict)))).Error.ShouldBe(_conflict);

    [Fact]
    public async Task BindAsync_does_not_call_the_function_on_failure()
    {
        var called = false;

        var fromResult = await Result.Failure<int>(_notFound).BindAsync(value => { called = true; return Loaded(value); });
        var fromTask = await Missing().BindAsync(value => { called = true; return Result.Success(value); });
        var fromTaskAsync = await Missing().BindAsync(value => { called = true; return Loaded(value); });

        called.ShouldBeFalse();
        fromResult.Error.ShouldBe(_notFound);
        fromTask.Error.ShouldBe(_notFound);
        fromTaskAsync.Error.ShouldBe(_notFound);
    }

    [Fact]
    public async Task MatchAsync_takes_the_right_branch()
    {
        (await Loaded(2).MatchAsync(value => $"value {value}", error => error.Code)).ShouldBe("value 2");
        (await Missing().MatchAsync(value => $"value {value}", error => error.Code)).ShouldBe("party.party-not-found");
    }
}
