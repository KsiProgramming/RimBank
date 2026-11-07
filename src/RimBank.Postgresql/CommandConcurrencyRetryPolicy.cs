using JasperFx;
using JasperFx.CodeGeneration;
using RimBank.SharedKernel;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace RimBank.Postgresql;

/// <summary>
/// Retries a context's command once when its commit loses an optimistic-concurrency race (ADR 0027). Added by
/// <see cref="ServiceDomainStoreRegistration.AddServiceDomainStore{TStore, TCore}"/> next to the idempotency policy.
/// </summary>
/// <remarks>
/// A handler decides against the stream version it loaded and appends with that version as the expected one. When
/// another writer commits in between, Marten refuses the commit with a <see cref="ConcurrencyException"/>, nothing of
/// the attempt is stored (events, outbox, idempotency outcome) and its transaction-scoped idempotency lock is released.
/// The retry runs the whole chain again: the handler reloads the stream and its own version check answers with the
/// context's conflict error as a <see cref="Result{T}"/>. Wolverine applies retries inside <c>InvokeAsync</c>, so the
/// caller (HTTP, MCP) never sees the exception.
/// </remarks>
internal sealed class CommandConcurrencyRetryPolicy(string coreNamespace) : IChainPolicy
{
    public void Apply(IReadOnlyList<IChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.OfType<HandlerChain>().Where(IsContextCommand))
        {
            chain.OnException<ConcurrencyException>().RetryTimes(1);
        }
    }

    private bool IsContextCommand(HandlerChain chain) =>
        typeof(ICommand).IsAssignableFrom(chain.MessageType)
        && chain.HandlerCalls().Any(call => call.HandlerType.Namespace is { } ns && (ns == coreNamespace || ns.StartsWith(coreNamespace + ".", StringComparison.Ordinal)));
}
