namespace RimBank.SharedKernel;

/// <summary>
/// Marks a command: an immutable record named as a BIAN operation in the imperative, such as <c>RegisterParty</c>
/// (write side, tech-stack §8.1, ADR 0012).
/// </summary>
/// <remarks>
/// <para>
/// A class with a <c>Handle</c> or <c>HandleAsync</c> method whose first parameter is an <see cref="ICommand"/> is a
/// command handler. The architecture tests check that it stays on the write side: it uses an <see cref="IWritePort"/>
/// and the aggregate's decisions, never an <see cref="IReadPort"/>, a view, a query or a query handler.
/// </para>
/// <para>
/// Every command carries an <see cref="SharedKernel.IdempotencyKey"/> (ADR 0018). The context's idempotency policy
/// handles a command once per key: sending it again with the same payload returns the first acknowledgement, and with
/// a different payload returns a <c>shared.idempotency-key-reused</c> conflict. The handler never reads the key.
/// </para>
/// </remarks>
public interface ICommand
{
    /// <summary>The caller's key for this request (ADR 0018).</summary>
    IdempotencyKey IdempotencyKey { get; }
}
