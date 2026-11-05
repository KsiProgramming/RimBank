namespace RimBank.SharedKernel;

/// <summary>
/// Async versions of <see cref="Result{T}.Map{TOut}"/>, <see cref="Result{T}.Bind{TOut}(Func{T, Result{TOut}})"/>
/// and <see cref="Result{T}.Match{TOut}"/>, so handlers can chain awaited ports without nesting.
/// A failure short-circuits: the next function is never called.
/// </summary>
public static class ResultAsyncExtensions
{
    /// <summary>On success, transforms the value with an async function.</summary>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Result<T> result, Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? Result.Success(await map(result.Value).ConfigureAwait(false))
            : Result.Failure<TOut>(result.Error);
    }

    /// <summary>Awaits the result, then on success transforms the value.</summary>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> result, Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (await result.ConfigureAwait(false)).Map(map);
    }

    /// <summary>Awaits the result, then on success transforms the value with an async function.</summary>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> result, Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        return await (await result.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);
    }

    /// <summary>On success, continues with an async step.</summary>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Result<T> result, Func<T, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? await next(result.Value).ConfigureAwait(false)
            : Result.Failure<TOut>(result.Error);
    }

    /// <summary>Awaits the result, then on success continues with the next step.</summary>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> result, Func<T, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (await result.ConfigureAwait(false)).Bind(next);
    }

    /// <summary>Awaits the result, then on success continues with an async step.</summary>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> result, Func<T, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        return await (await result.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);
    }

    /// <summary>Awaits the result, then returns <paramref name="onSuccess"/>(value) or <paramref name="onFailure"/>(error).</summary>
    public static async Task<TOut> MatchAsync<T, TOut>(this Task<Result<T>> result, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (await result.ConfigureAwait(false)).Match(onSuccess, onFailure);
    }
}
