namespace RimBank.SharedKernel.Testing;

/// <summary>
/// Given events / When command / Then events, for any <see cref="IDecider{TSelf, TCommand, TEvent}"/> (ADR 0010).
/// </summary>
/// <remarks>
/// C# cannot infer the command and event types from the state's interface, so name the three types once with an
/// alias in the test file:
/// <code>
/// using LedgerSpecification = RimBank.SharedKernel.Testing.DeciderSpecification&lt;Ledger, LedgerCommand, LedgerEvent&gt;;
///
/// LedgerSpecification.Given(new LedgerOpened(eur))
///     .When(new PostToLedger(tenEuros))
///     .Then(new LedgerPosted(tenEuros));
/// </code>
/// The given events are folded from <c>Initial</c> with <c>Evolve</c>. Events are compared with
/// <see cref="object.Equals(object?)"/>, in order.
/// </remarks>
/// <typeparam name="TState">The aggregate state.</typeparam>
/// <typeparam name="TCommand">The aggregate's command base type.</typeparam>
/// <typeparam name="TEvent">The aggregate's event base type.</typeparam>
public sealed class DeciderSpecification<TState, TCommand, TEvent>
    where TState : IDecider<TState, TCommand, TEvent>
{
    private readonly IReadOnlyList<TEvent> _given;

    private DeciderSpecification(IReadOnlyList<TEvent> given) => _given = given;

    /// <summary>The events already in the stream; none for a new aggregate.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The entry point of the specification; the type arguments are named once through an alias.")]
    public static DeciderSpecification<TState, TCommand, TEvent> Given(params TEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return new([.. events]);
    }

    /// <summary>Decides <paramref name="command"/> against the state folded from the given events.</summary>
    public DeciderOutcome<TCommand, TEvent> When(TCommand command)
    {
        var state = _given.Aggregate(TState.Initial, TState.Evolve);
        return new DeciderOutcome<TCommand, TEvent>(_given, command, TState.Decide(command, state));
    }
}

/// <summary>The outcome of <see cref="DeciderSpecification{TState, TCommand, TEvent}.When"/>, to assert on.</summary>
/// <typeparam name="TCommand">The aggregate's command base type.</typeparam>
/// <typeparam name="TEvent">The aggregate's event base type.</typeparam>
public sealed class DeciderOutcome<TCommand, TEvent>
{
    private readonly IReadOnlyList<TEvent> _given;
    private readonly TCommand _command;
    private readonly Result<IReadOnlyList<TEvent>> _result;

    internal DeciderOutcome(IReadOnlyList<TEvent> given, TCommand command, Result<IReadOnlyList<TEvent>> result)
    {
        _given = given;
        _command = command;
        _result = result;
    }

    /// <summary>
    /// Asserts that the command was accepted and decided exactly <paramref name="expected"/>, in order.
    /// <c>Then()</c> with no events asserts an accepted no-op.
    /// </summary>
    /// <exception cref="ShouldAssertException">The command was rejected, or decided other events.</exception>
    public void Then(params TEvent[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        if (_result.IsFailure)
        {
            throw Fail(
                Describe.Section("Expected events", Describe.Events(expected)),
                Describe.Section("But the command was rejected with", Describe.Value(_result.Error)));
        }

        var actual = _result.Value;
        if (!actual.SequenceEqual(expected))
        {
            var expectedText = Describe.Events(expected);
            var actualText = Describe.Events(actual);
            throw Fail(
                Describe.Section("Expected events", expectedText),
                Describe.Section("Actual events", actualText),
                Describe.SameTextHint(expectedText, actualText));
        }
    }

    /// <summary>Asserts that the command was rejected with an error of <paramref name="kind"/> and <paramref name="code"/>.</summary>
    /// <returns>The error, for further assertions (for example on its field errors).</returns>
    /// <exception cref="ShouldAssertException">The command was accepted, or rejected with another error.</exception>
    public Error ThenFails(ErrorKind kind, string code)
    {
        if (_result.IsSuccess)
        {
            throw Fail(
                Describe.Section("Expected the command to be rejected with", $"  {kind} {code}"),
                Describe.Section("But it was accepted with events", Describe.Events(_result.Value)));
        }

        var error = _result.Error;
        if (error.Kind != kind || error.Code != code)
        {
            throw Fail(
                Describe.Section("Expected the command to be rejected with", $"  {kind} {code}"),
                Describe.Section("But it was rejected with", Describe.Value(error)));
        }

        return error;
    }

    private ShouldAssertException Fail(params string?[] outcome) =>
        new(Describe.Join(
            new[]
            {
                Describe.Section("Given", Describe.Events(_given)),
                Describe.Section("When", Describe.Value(_command)),
            }.Concat(outcome)));
}
