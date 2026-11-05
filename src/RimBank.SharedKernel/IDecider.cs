namespace RimBank.SharedKernel;

/// <summary>
/// The write-side contract of an event-sourced aggregate (mission principle 3, tech-stack §7 and §8.1, ADR 0010):
/// <c>Decide(command, state) → events</c> and <c>Evolve(state, event) → state</c>.
/// </summary>
/// <typeparam name="TSelf">The aggregate state, a <c>sealed record</c> that implements this interface.</typeparam>
/// <typeparam name="TCommand">The aggregate's own command base type, matched with a switch expression.</typeparam>
/// <typeparam name="TEvent">The aggregate's own domain event base type, matched with a switch expression.</typeparam>
/// <remarks>
/// <para>
/// Every member is <c>static</c>, so a decider cannot hold services, ports or a <see cref="TimeProvider"/>:
/// purity is structural. Time and new identifiers come in the command, set by the handler.
/// </para>
/// <para>
/// The state is the fold of the stream's events: <c>events.Aggregate(Initial, Evolve)</c>. Put the state and
/// <see cref="Evolve"/> in <c>&lt;Aggregate&gt;.cs</c> and <see cref="Decide"/> in a <c>partial</c>,
/// <c>Decisions.cs</c> (tech-stack §3.4).
/// </para>
/// </remarks>
public interface IDecider<TSelf, TCommand, TEvent>
    where TSelf : IDecider<TSelf, TCommand, TEvent>
{
    /// <summary>The state before the first event, for example "not registered". Never null.</summary>
    static abstract TSelf Initial { get; }

    /// <summary>
    /// Decides what happens when <paramref name="command"/> meets <paramref name="state"/>. Pure.
    /// </summary>
    /// <returns>
    /// The events to append (empty for an accepted no-op, such as a repeated close), built with
    /// <see cref="Decision.Accept{TEvent}"/> or <see cref="Decision.Nothing{TEvent}"/>; or the business
    /// <see cref="Error"/> that rejects the command.
    /// </returns>
    static abstract Result<IReadOnlyList<TEvent>> Decide(TCommand command, TSelf state);

    /// <summary>
    /// Applies one of the aggregate's own events to <paramref name="state"/>. Pure and total over the events
    /// <see cref="Decide"/> can return; an event the aggregate does not know is a programmer error and throws
    /// <see cref="System.Diagnostics.UnreachableException"/>.
    /// </summary>
    static abstract TSelf Evolve(TSelf state, TEvent domainEvent);
}
