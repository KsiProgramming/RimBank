namespace RimBank.SharedKernel;

/// <summary>
/// The outcome of an operation that returns no value: a success, or a failure carrying an <see cref="SharedKernel.Error"/>.
/// Used by commands that only acknowledge. Also the entry point for creating a <see cref="Result{T}"/>.
/// </summary>
/// <remarks>Expected business failures are values, not exceptions (mission principle 3, ADR 0006).</remarks>
public sealed class Result
{
    private static readonly Result _success = new(null);

    private readonly Error? _error;

    private Result(Error? error) => _error = error;

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess => _error is null;

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure. Reading it on a success is a programmer error.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>A success with no value.</summary>
    public static Result Success() => _success;

    /// <summary>A failure with the given error.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    /// <summary>A success holding <paramref name="value"/>, which must not be null.</summary>
    public static Result<T> Success<T>(T value) => new(value);

    /// <summary>A failure of type <typeparamref name="T"/> with the given error.</summary>
    public static Result<T> Failure<T>(Error error) => new(error);

    /// <summary>Converts an error into a failed result.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Returns <paramref name="onSuccess"/>() or <paramref name="onFailure"/>(error).</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        _error is null ? onSuccess() : onFailure(_error);

    /// <summary>On success, produces a value; a failure passes through.</summary>
    public Result<TOut> Map<TOut>(Func<TOut> map) =>
        _error is null ? Success(map()) : Failure<TOut>(_error);

    /// <summary>On success, continues with <paramref name="next"/>; a failure passes through.</summary>
    public Result Bind(Func<Result> next) => _error is null ? next() : this;

    /// <summary>On success, continues with <paramref name="next"/>; a failure passes through.</summary>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> next) =>
        _error is null ? next() : Failure<TOut>(_error);

    /// <summary>On failure, replaces the error; a success passes through.</summary>
    public Result MapError(Func<Error, Error> map) => _error is null ? this : Failure(map(_error));

    /// <summary>On success, runs <paramref name="action"/>; returns this result either way.</summary>
    public Result Tap(Action action)
    {
        if (_error is null)
        {
            action();
        }

        return this;
    }

    /// <inheritdoc />
    public override string ToString() => _error is null ? "Success" : $"Failure({_error})";
}
