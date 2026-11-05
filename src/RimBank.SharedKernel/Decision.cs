namespace RimBank.SharedKernel;

/// <summary>
/// Builds the successful outcome of <see cref="IDecider{TSelf, TCommand, TEvent}.Decide"/>. A rejection needs no
/// helper: an <see cref="Error"/> converts to the result implicitly.
/// </summary>
/// <remarks>
/// C# does not apply user-defined conversions to interface types, so an event array cannot become a
/// <c>Result&lt;IReadOnlyList&lt;TEvent&gt;&gt;</c> implicitly. Name the event base type:
/// <c>Decision.Accept&lt;LedgerEvent&gt;(new LedgerOpened(currency))</c>.
/// </remarks>
public static class Decision
{
    /// <summary>The command is accepted and produces <paramref name="events"/>, in order.</summary>
    public static Result<IReadOnlyList<TEvent>> Accept<TEvent>(params TEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return Result.Success<IReadOnlyList<TEvent>>(Array.AsReadOnly([.. events]));
    }

    /// <summary>The command is accepted and changes nothing, for example a repeated close (an idempotent no-op).</summary>
    public static Result<IReadOnlyList<TEvent>> Nothing<TEvent>() =>
        Result.Success<IReadOnlyList<TEvent>>(Array.Empty<TEvent>());
}
