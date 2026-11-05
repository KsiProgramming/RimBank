namespace RimBank.SharedKernel;

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="T"/>: a success holding a value,
/// or a failure carrying an <see cref="SharedKernel.Error"/>. Create one with <see cref="Result.Success{T}(T)"/>,
/// <see cref="Result.Failure{T}(SharedKernel.Error)"/> or an implicit conversion.
/// </summary>
/// <remarks>
/// A success always has a value: absence is a failure (usually <see cref="ErrorKind.NotFound"/>), never null.
/// Expected business failures are values, not exceptions (mission principle 3, ADR 0006).
/// </remarks>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    internal Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    internal Result(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess => _error is null;

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The value. Reading it on a failure is a programmer error.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Value => _error is null
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value. It failed with {_error}.");

    /// <summary>The failure. Reading it on a success is a programmer error.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>Converts a value into a successful result.</summary>
    public static implicit operator Result<T>(T value) => new(value);

    /// <summary>Converts an error into a failed result.</summary>
    public static implicit operator Result<T>(Error error) => new(error);

    /// <summary>Returns <paramref name="onSuccess"/>(value) or <paramref name="onFailure"/>(error).</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        _error is null ? onSuccess(_value!) : onFailure(_error);

    /// <summary>On success, transforms the value; a failure passes through.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        _error is null ? new Result<TOut>(map(_value!)) : new Result<TOut>(_error);

    /// <summary>On success, continues with <paramref name="next"/>; a failure passes through.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) =>
        _error is null ? next(_value!) : new Result<TOut>(_error);

    /// <summary>On success, continues with an operation that returns no value; a failure passes through.</summary>
    public Result Bind(Func<T, Result> next) =>
        _error is null ? next(_value!) : Result.Failure(_error);

    /// <summary>On failure, replaces the error; a success passes through.</summary>
    public Result<T> MapError(Func<Error, Error> map) =>
        _error is null ? this : new Result<T>(map(_error));

    /// <summary>On success, runs <paramref name="action"/> with the value; returns this result either way.</summary>
    public Result<T> Tap(Action<T> action)
    {
        if (_error is null)
        {
            action(_value!);
        }

        return this;
    }

    /// <inheritdoc />
    public override string ToString() => _error is null ? $"Success({_value})" : $"Failure({_error})";
}
