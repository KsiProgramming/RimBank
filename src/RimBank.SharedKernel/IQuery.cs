using System.Diagnostics.CodeAnalysis;

namespace RimBank.SharedKernel;

/// <summary>
/// Marks a query: an immutable record named <c>Retrieve&lt;Thing&gt;</c> or <c>List&lt;Things&gt;</c> whose handler
/// returns a <see cref="Result{T}"/> of <typeparamref name="TView"/> (read side, tech-stack §8.2, ADR 0012).
/// </summary>
/// <typeparam name="TView">The view (read model) the query returns.</typeparam>
/// <remarks>
/// A class with a <c>Handle</c> or <c>HandleAsync</c> method whose first parameter is an <see cref="IQuery{TView}"/>
/// is a query handler. The architecture tests check that it reads only through an <see cref="IReadPort"/>, never an
/// <see cref="IWritePort"/>, an aggregate (<see cref="IDecider{TSelf, TCommand, TEvent}"/>), a command or a
/// command handler.
/// </remarks>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "A marker the architecture tests classify types by (ADR 0012).")]
public interface IQuery<TView>
    where TView : class;
