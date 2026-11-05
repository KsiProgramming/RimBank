namespace RimBank.SharedKernel.Testing;

/// <summary>
/// Given events / Then view, for any <see cref="IProjection{TView, TEvent}"/> (ADR 0010).
/// </summary>
/// <remarks>
/// Name the three types once with an alias in the test file:
/// <code>
/// using LedgerViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification&lt;LedgerViewProjection, LedgerView, LedgerEvent&gt;;
///
/// LedgerViewSpecification.Given(new LedgerOpened(eur), new LedgerPosted(tenEuros))
///     .Then(new LedgerView(tenEuros, Postings: 1));
/// </code>
/// The events are folded from <c>null</c> with <c>Project</c>, the same way a rebuild does. Views are compared with
/// <see cref="object.Equals(object?)"/>.
/// </remarks>
/// <typeparam name="TProjection">The projection under test.</typeparam>
/// <typeparam name="TView">The view it produces.</typeparam>
/// <typeparam name="TEvent">The event base type it projects.</typeparam>
public sealed class ProjectionSpecification<TProjection, TView, TEvent>
    where TProjection : IProjection<TView, TEvent>
    where TView : class
{
    private readonly IReadOnlyList<TEvent> _given;
    private readonly TView? _actual;

    private ProjectionSpecification(IReadOnlyList<TEvent> given)
    {
        _given = given;
        _actual = given.Aggregate((TView?)null, TProjection.Project);
    }

    /// <summary>The events to project, in stream order.</summary>
    public static ProjectionSpecification<TProjection, TView, TEvent> Given(params TEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return new([.. events]);
    }

    /// <summary>Asserts that the projected view equals <paramref name="expected"/>.</summary>
    /// <exception cref="ShouldAssertException">There is no view, or it differs.</exception>
    public void Then(TView expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        if (!Equals(_actual, expected))
        {
            var expectedText = Describe.Value(expected);
            var actualText = Describe.Value(_actual);
            throw Fail(
                Describe.Section("Expected view", expectedText),
                Describe.Section("Actual view", actualText),
                _actual is null ? null : Describe.SameTextHint(expectedText, actualText));
        }
    }

    /// <summary>Asserts that the events produce no view (not created yet, or removed).</summary>
    /// <exception cref="ShouldAssertException">A view was produced.</exception>
    public void ThenNoView()
    {
        if (_actual is not null)
        {
            throw Fail(
                Describe.Section("Expected no view", "  (none)"),
                Describe.Section("Actual view", Describe.Value(_actual)));
        }
    }

    private ShouldAssertException Fail(params string?[] outcome) =>
        new(Describe.Join(new[] { Describe.Section("Given", Describe.Events(_given)) }.Concat(outcome)));
}
