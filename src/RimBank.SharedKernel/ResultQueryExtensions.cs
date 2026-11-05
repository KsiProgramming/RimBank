namespace RimBank.SharedKernel;

/// <summary>
/// LINQ query syntax over <see cref="Result{T}"/>: <c>from a in r1 from b in r2 select …</c>.
/// The first failure stops the chain and is returned.
/// </summary>
public static class ResultQueryExtensions
{
    /// <summary>Same as <see cref="Result{T}.Map{TOut}"/>.</summary>
    public static Result<TOut> Select<T, TOut>(this Result<T> result, Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Map(selector);
    }

    /// <summary>Same as <see cref="Result{T}.Bind{TOut}(Func{T, Result{TOut}})"/>, followed by a projection.</summary>
    public static Result<TOut> SelectMany<T, TNext, TOut>(
        this Result<T> result,
        Func<T, Result<TNext>> next,
        Func<T, TNext, TOut> project)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Bind(value => next(value).Map(nextValue => project(value, nextValue)));
    }
}
