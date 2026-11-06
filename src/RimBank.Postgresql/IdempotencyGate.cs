using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marten;
using RimBank.SharedKernel;
using Wolverine.Runtime;

namespace RimBank.Postgresql;

/// <summary>
/// The steps the idempotency policy adds around every command handler of a context (ADR 0018). Wolverine's generated
/// handler code calls them; a context never calls them itself.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EnterAsync{TAcknowledgement}"/> runs before the handler, inside a transaction on the handler's own
/// session. It takes a transaction-scoped PostgreSQL advisory lock on the key, then looks for the outcome of an earlier
/// command with the same key:
/// </para>
/// <list type="bullet">
/// <item>none: the handler runs, and <see cref="Remember{TAcknowledgement}"/> stores its acknowledgement in the same
/// session when it succeeds, so it commits with the events and the outbox;</item>
/// <item>same fingerprint: the stored acknowledgement is returned and the handler does not run;</item>
/// <item>different fingerprint: <c>shared.idempotency-key-reused</c> is returned;</item>
/// <item>the lock is held by a command still being handled: <c>shared.idempotency-key-in-use</c> is returned.</item>
/// </list>
/// <para>
/// A key is scoped to its context, its command type and its caller: the user name on the envelope, which the host's
/// actor relay sets to the caller's subject, or <see cref="Actor.System"/> for a message no caller sent (ADR 0019). Two
/// callers that use the same key never replay, refuse or block each other's commands.
/// </para>
/// <para>
/// A command without a key returns <c>shared.idempotency-key-missing</c>. Every refusal and every replay is returned
/// the way a handler returns its result, so <c>IMessageBus.InvokeAsync&lt;Result&lt;T&gt;&gt;</c> receives it as a
/// value; nothing is committed for them.
/// </para>
/// </remarks>
public static class IdempotencyGate
{
    private const string KeyProperty = nameof(ICommand.IdempotencyKey);

    /// <summary>Decides whether the handler runs. Called by generated code only.</summary>
    /// <typeparam name="TAcknowledgement">The value of the handler's <see cref="Result{T}"/>.</typeparam>
    /// <param name="session">The handler's own session on the context's store.</param>
    /// <param name="command">The command being handled.</param>
    /// <param name="context">The message context, which carries a replay or a refusal back to the caller.</param>
    /// <param name="scope">The context's schema, which scopes the advisory lock.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The ticket the generated code checks before calling the handler.</returns>
    public static async Task<IdempotencyTicket> EnterAsync<TAcknowledgement>(
        IDocumentSession session,
        ICommand command,
        MessageContext context,
        string scope,
        CancellationToken cancellationToken)
        where TAcknowledgement : notnull
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        if (command.IdempotencyKey.IsDefault)
        {
            return await StopAsync(context, (Result<TAcknowledgement>)IdempotencyKey.Missing);
        }

        var id = OutcomeId(command, CallerOf(context));
        await session.BeginTransactionAsync(cancellationToken);
        var locked = await session.QueryAsync<bool>("select pg_try_advisory_xact_lock(?)", cancellationToken, LockKey(scope, id));
        if (!locked.Single())
        {
            return await StopAsync(context, (Result<TAcknowledgement>)IdempotencyKey.InUse);
        }

        var fingerprint = Fingerprint(command);
        var earlier = await session.LoadAsync<IdempotentOutcome>(id, cancellationToken);
        if (earlier is null)
        {
            return new IdempotencyTicket(Proceed: true, id, fingerprint);
        }

        Result<TAcknowledgement> replay = earlier.Fingerprint == fingerprint
            ? JsonSerializer.Deserialize<TAcknowledgement>(earlier.Acknowledgement)
                ?? throw new InvalidOperationException($"The stored acknowledgement of {id} is empty.")
            : IdempotencyKey.Reused;
        return await StopAsync(context, replay);
    }

    /// <summary>Stores the handler's acknowledgement when it succeeded. Called by generated code only.</summary>
    /// <typeparam name="TAcknowledgement">The value of the handler's <see cref="Result{T}"/>.</typeparam>
    /// <param name="session">The handler's own session, saved after this call by the transactional middleware.</param>
    /// <param name="ticket">The ticket <see cref="EnterAsync{TAcknowledgement}"/> returned.</param>
    /// <param name="result">The handler's result.</param>
    public static void Remember<TAcknowledgement>(IDocumentSession session, IdempotencyTicket ticket, Result<TAcknowledgement> result)
        where TAcknowledgement : notnull
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(result);

        // A failed decision commits nothing (ADR 0017), so the same key can be retried once its cause is fixed.
        if (result.IsSuccess)
        {
            session.Insert(new IdempotentOutcome
            {
                Id = ticket.OutcomeId,
                Fingerprint = ticket.Fingerprint,
                Acknowledgement = JsonSerializer.Serialize(result.Value),
            });
        }
    }

    /// <summary>
    /// The hexadecimal SHA-256 of the command's JSON without its key: the same payload always has the same fingerprint,
    /// and any change to it gives another one.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>64 lower-case hexadecimal characters.</returns>
    public static string Fingerprint(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var payload = JsonSerializer.SerializeToNode(command, command.GetType()) as JsonObject
            ?? throw new InvalidOperationException($"{command.GetType().Name} must serialize to a JSON object.");
        payload.Remove(KeyProperty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToJsonString())));
    }

    /// <summary>The caller a command is scoped to: the envelope's user name, or the system actor when it has none.</summary>
    /// <param name="context">The message context of the command.</param>
    /// <returns>The caller's subject.</returns>
    internal static string CallerOf(MessageContext context) =>
        context.Envelope?.UserName is { Length: > 0 } caller ? caller : Actor.System.Subject;

    // <command type>:<caller length>:<caller>:<key>. The length keeps the id unambiguous: a subject and a key may both
    // contain colons, and "u:x" + "k" must never read like "u" + "x:k".
    internal static string OutcomeId(ICommand command, string caller) =>
        $"{command.GetType().FullName}:{caller.Length}:{caller}:{command.IdempotencyKey.Value}";

    // Advisory locks are shared by the whole database, so the key includes the context's schema.
    internal static long LockKey(string scope, string outcomeId) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope}|{outcomeId}")));

    private static async Task<IdempotencyTicket> StopAsync<TAcknowledgement>(MessageContext context, Result<TAcknowledgement> result)
    {
        // The same call Wolverine generates for a handler's return value: it becomes the response of InvokeAsync<T>.
        await context.EnqueueCascadingAsync(result);
        return IdempotencyTicket.Stop;
    }
}

/// <summary>Whether the handler runs, and what <see cref="IdempotencyGate.Remember{TAcknowledgement}"/> stores if it succeeds.</summary>
/// <param name="Proceed">True when the handler runs.</param>
/// <param name="OutcomeId">The id of the outcome to store.</param>
/// <param name="Fingerprint">The fingerprint of the command's payload.</param>
public sealed record IdempotencyTicket(bool Proceed, string OutcomeId, string Fingerprint)
{
    /// <summary>The handler does not run: a replay or a refusal was already returned.</summary>
    public static IdempotencyTicket Stop { get; } = new(Proceed: false, string.Empty, string.Empty);
}
