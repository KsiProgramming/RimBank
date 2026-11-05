using FsCheck;
using FsCheck.Fluent;

namespace RimBank.SharedKernel.Testing;

/// <summary>
/// FsCheck laws every <see cref="IDecider{TSelf, TCommand, TEvent}"/> must follow, and the agreement law between an
/// aggregate and a projection of its events (tech-stack §9, ADR 0010).
/// </summary>
/// <remarks>
/// <para>
/// Each law runs random command sequences from <c>Initial</c>: every command is decided against the current state,
/// and an accepted command's events are evolved before the next command. Rejected commands are kept in the
/// sequence, so the laws also cover rejections. A failing sequence shrinks by dropping commands.
/// </para>
/// <para>
/// Pass a generator that produces both valid and invalid commands for the reachable states. Use them from a
/// <c>[Property]</c> method that returns the <see cref="Property"/>:
/// <code>
/// [Property]
/// public Property Decisions_are_deterministic() =&gt;
///     DeciderLaws&lt;Ledger, LedgerCommand, LedgerEvent&gt;.DecisionsAreDeterministic(LedgerCommands);
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TState">The aggregate state.</typeparam>
/// <typeparam name="TCommand">The aggregate's command base type.</typeparam>
/// <typeparam name="TEvent">The aggregate's event base type.</typeparam>
public static class DeciderLaws<TState, TCommand, TEvent>
    where TState : IDecider<TState, TCommand, TEvent>
{
    /// <summary>The same commands from <c>Initial</c> always give the same outcomes, events, errors and states.</summary>
    public static Property DecisionsAreDeterministic(Gen<TCommand> commands) =>
        Prop.ForAll(Sequences(commands), sequence =>
        {
            var first = Run(sequence);
            var second = Run(sequence);
            for (var i = 0; i < first.Count; i++)
            {
                if (!first[i].SameAs(second[i]))
                {
                    throw new ShouldAssertException(Describe.Join(
                    [
                        Describe.Section("Commands so far", Describe.Events(sequence.Take(i + 1))),
                        Describe.Section("First run", Describe.Value(first[i])),
                        Describe.Section("Second run", Describe.Value(second[i])),
                        "Decide and Evolve must be pure: the same inputs must give the same outputs.",
                    ]));
                }
            }
        });

    /// <summary>Every event <c>Decide</c> returns, from any reachable state, can be applied by <c>Evolve</c>.</summary>
    public static Property DecidedEventsEvolve(Gen<TCommand> commands) =>
        Prop.ForAll(Sequences(commands), sequence => { _ = Run(sequence); });

    /// <summary>
    /// After every command, the view projected from all accepted events equals <paramref name="expectedView"/> of
    /// the aggregate state: the read side shows what the write side decided (for example a projected balance equals
    /// the aggregate's balance).
    /// </summary>
    /// <typeparam name="TProjection">The projection of the aggregate's events.</typeparam>
    /// <typeparam name="TView">The view it produces.</typeparam>
    /// <param name="commands">The command generator.</param>
    /// <param name="expectedView">The view the state should project to; <c>null</c> when there should be no view.</param>
    public static Property ProjectionAgreesWithState<TProjection, TView>(Gen<TCommand> commands, Func<TState, TView?> expectedView)
        where TProjection : IProjection<TView, TEvent>
        where TView : class
    {
        ArgumentNullException.ThrowIfNull(expectedView);

        return Prop.ForAll(Sequences(commands), sequence =>
        {
            TView? view = null;
            var accepted = new List<TEvent>();
            foreach (var step in Run(sequence))
            {
                foreach (var domainEvent in step.Events)
                {
                    view = TProjection.Project(view, domainEvent);
                    accepted.Add(domainEvent);
                }

                var expected = expectedView(step.State);
                if (!Equals(view, expected))
                {
                    throw new ShouldAssertException(Describe.Join(
                    [
                        Describe.Section("Accepted events", Describe.Events(accepted)),
                        Describe.Section("Aggregate state", Describe.Value(step.State)),
                        Describe.Section("Expected view (from the state)", Describe.Value(expected)),
                        Describe.Section("Projected view", Describe.Value(view)),
                    ]));
                }
            }
        });
    }

    private static Arbitrary<TCommand[]> Sequences(Gen<TCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return Arb.From(Gen.ArrayOf(commands), ShrinkSequence);
    }

    // Drop one command at a time, so a failure shrinks to the shortest sequence that still fails.
    private static IEnumerable<TCommand[]> ShrinkSequence(TCommand[] sequence) =>
        Enumerable.Range(0, sequence.Length)
            .Select(skipped => sequence.Where((_, index) => index != skipped).ToArray());

    private static List<Step> Run(TCommand[] sequence)
    {
        var state = TState.Initial;
        var accepted = new List<TEvent>();
        var steps = new List<Step>(sequence.Length);
        foreach (var command in sequence)
        {
            var result = TState.Decide(command, state);
            var events = result.IsSuccess ? result.Value : [];
            foreach (var domainEvent in events)
            {
                try
                {
                    state = TState.Evolve(state, domainEvent);
                }
                catch (Exception exception) when (exception is not ShouldAssertException)
                {
                    throw new ShouldAssertException(Describe.Join(
                    [
                        Describe.Section("Accepted events", Describe.Events(accepted)),
                        Describe.Section("When", Describe.Value(command)),
                        Describe.Section("Evolve threw for the decided event", Describe.Value(domainEvent)),
                        Describe.Value(exception),
                    ]));
                }

                accepted.Add(domainEvent);
            }

            steps.Add(new Step(command, result.IsSuccess ? null : result.Error, events, state));
        }

        return steps;
    }

    private sealed record Step(TCommand Command, Error? Rejection, IReadOnlyList<TEvent> Events, TState State)
    {
        public bool SameAs(Step other) =>
            Equals(Command, other.Command)
            && Equals(Rejection, other.Rejection)
            && Events.SequenceEqual(other.Events)
            && Equals(State, other.State);

        public override string ToString() =>
            Rejection is null
                ? $"accepted with [{string.Join(", ", Events)}] → {State}"
                : $"rejected with {Rejection} → {State}";
    }
}
