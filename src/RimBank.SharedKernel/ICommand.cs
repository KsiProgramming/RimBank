namespace RimBank.SharedKernel;

/// <summary>
/// Marks a command: an immutable record named as a BIAN operation in the imperative, such as <c>RegisterParty</c>
/// (write side, tech-stack §8.1, ADR 0012).
/// </summary>
/// <remarks>
/// A class with a <c>Handle</c> or <c>HandleAsync</c> method whose first parameter is an <see cref="ICommand"/> is a
/// command handler. The architecture tests check that it stays on the write side: it uses an <see cref="IWritePort"/>
/// and the aggregate's decisions, never an <see cref="IReadPort"/>, a view, a query or a query handler.
/// </remarks>
public interface ICommand;
