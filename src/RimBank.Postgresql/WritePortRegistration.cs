using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;
using Marten;
using Microsoft.Extensions.Hosting;
using RimBank.SharedKernel;
using Wolverine;
using Wolverine.Attributes;
using Wolverine.Configuration;

namespace RimBank.Postgresql;

/// <summary>
/// Gives a core handler its context's write port, implemented over the session Wolverine opens for the handler's
/// transaction (ADR 0017).
/// </summary>
/// <remarks>
/// A core handler takes its write port as a parameter (specs/tech-stack.md §4) and never sees Marten or Wolverine. The
/// port's Marten implementation needs the handler's own <see cref="IDocumentSession"/>: the one opened on the
/// context's store, enrolled in the outbox, and saved by the transactional middleware after the handler returns, so
/// the events and the outgoing messages commit together. With only ancillary stores that session is not in the
/// container, so the port is not registered there either: Wolverine's generated handler code builds it inline,
/// <c>new TImplementation(documentSession)</c>, and every handler that takes it is made transactional.
/// </remarks>
public static class WritePortRegistration
{
    /// <summary>Registers <typeparamref name="TImplementation"/> as the context's <typeparamref name="TPort"/>.</summary>
    /// <typeparam name="TPort">The write port, declared in the context's core.</typeparam>
    /// <typeparam name="TImplementation">Its Marten implementation, in the context's <c>.Postgresql</c>, with a public constructor that takes only an <see cref="IDocumentSession"/>.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TImplementation"/> has no constructor that takes only an <see cref="IDocumentSession"/>.</exception>
    public static IHostApplicationBuilder AddWritePort<TPort, TImplementation>(this IHostApplicationBuilder builder)
        where TPort : class, IWritePort
        where TImplementation : class, TPort
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (typeof(TImplementation).GetConstructor([typeof(IDocumentSession)]) is null)
        {
            throw new ArgumentException(
                $"{typeof(TImplementation).Name} must have a public constructor that takes only an IDocumentSession: the handler's own session (ADR 0017).",
                nameof(TImplementation));
        }

        builder.Services.ConfigureWolverine(options =>
        {
            options.CodeGeneration.Sources.Add(new WritePortSource(typeof(TPort), typeof(TImplementation)));
            options.Policies.Add(new WritePortTransactionPolicy(typeof(TPort)));
        });
        return builder;
    }

    private sealed class WritePortSource(Type port, Type implementation) : IVariableSource
    {
        public bool Matches(Type type) => type == port;

        public Variable Create(Type type) => new WritePortFrame(port, implementation).Port;
    }

    private sealed class WritePortFrame : SyncFrame
    {
        private readonly Type _implementation;
        private Variable? _session;

        public WritePortFrame(Type port, Type implementation)
        {
            _implementation = implementation;
            Port = new Variable(port, this);
        }

        public Variable Port { get; }

        public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
        {
            _session = chain.FindVariable(typeof(IDocumentSession));
            yield return _session;
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.Write($"var {Port.Usage} = new {_implementation.FullNameInCode()}({_session!.Usage});");
            Next?.GenerateCode(method, writer);
        }
    }

    // A handler that takes the write port writes through the session, so it always runs in a transaction, even when
    // nothing else in its signature tells Wolverine that it persists anything.
    private sealed class WritePortTransactionPolicy(Type port) : IChainPolicy
    {
        public void Apply(IReadOnlyList<IChain> chains, GenerationRules rules, IServiceContainer container)
        {
            foreach (var chain in chains.Where(chain => chain.HandlerCalls().Any(call => call.Method.GetParameters().Any(parameter => parameter.ParameterType == port))))
            {
                // The port is not in the container, so Wolverine cannot see on its own that it needs the session.
                chain.AddDependencyType(typeof(IDocumentSession));
                if (!chain.IsTransactional)
                {
                    new TransactionalAttribute().Modify(chain, rules, container);
                }
            }
        }
    }
}
