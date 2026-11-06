using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;
using Marten;
using RimBank.SharedKernel;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace RimBank.Postgresql;

/// <summary>
/// Wraps every command handler of one context with <see cref="IdempotencyGate"/> (ADR 0018). Added by
/// <see cref="ServiceDomainStoreRegistration.AddServiceDomainStore{TStore, TCore}"/>, so a context gets it with its
/// store and its core never sees it.
/// </summary>
/// <remarks>
/// The generated handler code becomes:
/// <code>
/// var ticket = await IdempotencyGate.EnterAsync&lt;TAck&gt;(documentSession, command, context, "schema", cancellation);
/// if (!ticket.Proceed) return;
/// var result = Handler.Handle(command, ...);
/// IdempotencyGate.Remember&lt;TAck&gt;(documentSession, ticket, result);
/// await documentSession.SaveChangesAsync(cancellation); // the transactional middleware
/// </code>
/// Queries are not touched: they change nothing and carry no key.
/// </remarks>
internal sealed class CommandIdempotencyPolicy(string coreNamespace, string scope) : IChainPolicy
{
    public void Apply(IReadOnlyList<IChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.OfType<HandlerChain>().Where(IsContextCommand))
        {
            var handler = chain.HandlerCalls().Single();
            var acknowledgement = AcknowledgementOf(handler.Method.ReturnType)
                ?? throw new InvalidOperationException(
                    $"{handler.HandlerType.Name}.{handler.Method.Name} handles the command {chain.MessageType.Name} and must return Result<T>, " +
                    "the acknowledgement the idempotency policy stores and replays (specs/tech-stack.md §8.1, ADR 0018).");

            // A command always writes, so its handler always runs in a transaction on its context's session.
            chain.AddDependencyType(typeof(IDocumentSession));
            if (!chain.IsTransactional)
            {
                new TransactionalAttribute().Modify(chain, rules, container);
            }

            var gate = new EnterFrame(chain.MessageType, acknowledgement, scope);
            chain.Middleware.Add(gate);

            // First of the postprocessors, so the outcome is in the session before the transaction saves it.
            chain.Postprocessors.Insert(0, new RememberFrame(acknowledgement, gate.Ticket));
        }
    }

    private bool IsContextCommand(HandlerChain chain) =>
        typeof(ICommand).IsAssignableFrom(chain.MessageType)
        && chain.HandlerCalls().Any(call => call.HandlerType.Namespace is { } ns && (ns == coreNamespace || ns.StartsWith(coreNamespace + ".", StringComparison.Ordinal)));

    // Result<T>, Task<Result<T>>, ValueTask<Result<T>>, or a tuple holding one (a result plus cascaded messages).
    private static Type? AcknowledgementOf(Type returnType)
    {
        if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>) || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            return AcknowledgementOf(returnType.GetGenericArguments()[0]);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType.IsValueTuple()
            ? returnType.GetGenericArguments().Select(AcknowledgementOf).FirstOrDefault(found => found is not null)
            : null;
    }

    private sealed class EnterFrame : AsyncFrame
    {
        private readonly Type _command;
        private readonly Type _acknowledgement;
        private readonly string _scope;
        private Variable? _session;
        private Variable? _message;
        private Variable? _context;
        private Variable? _cancellation;

        public EnterFrame(Type command, Type acknowledgement, string scope)
        {
            _command = command;
            _acknowledgement = acknowledgement;
            _scope = scope;
            Ticket = new Variable(typeof(IdempotencyTicket), "idempotencyTicket", this);
        }

        public Variable Ticket { get; }

        public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
        {
            _session = chain.FindVariable(typeof(IDocumentSession));
            yield return _session;
            _message = chain.FindVariable(_command);
            yield return _message;
            _context = chain.FindVariable(typeof(MessageContext));
            yield return _context;
            _cancellation = chain.FindVariable(typeof(CancellationToken));
            yield return _cancellation;
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.WriteComment("Idempotency (ADR 0018): replay or refuse a command whose key was already used.");
            writer.Write(
                $"var {Ticket.Usage} = await {typeof(IdempotencyGate).FullNameInCode()}.{nameof(IdempotencyGate.EnterAsync)}<{_acknowledgement.FullNameInCode()}>(" +
                $"{_session!.Usage}, {_message!.Usage}, {_context!.Usage}, \"{_scope}\", {_cancellation!.Usage});");
            writer.Write($"if (!{Ticket.Usage}.{nameof(IdempotencyTicket.Proceed)}) return;");
            Next?.GenerateCode(method, writer);
        }
    }

    private sealed class RememberFrame(Type acknowledgement, Variable ticket) : SyncFrame
    {
        private Variable? _session;
        private Variable? _result;

        public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
        {
            _session = chain.FindVariable(typeof(IDocumentSession));
            yield return _session;
            yield return ticket;
            _result = chain.FindVariable(typeof(Result<>).MakeGenericType(acknowledgement));
            yield return _result;
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.Write(
                $"{typeof(IdempotencyGate).FullNameInCode()}.{nameof(IdempotencyGate.Remember)}<{acknowledgement.FullNameInCode()}>(" +
                $"{_session!.Usage}, {ticket.Usage}, {_result!.Usage});");
            Next?.GenerateCode(method, writer);
        }
    }
}
