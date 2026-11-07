using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Postgresql;

/// <summary>
/// Gives a core query handler its context's read port, implemented over query sessions of the context's own store
/// (specs/tech-stack.md §8.2, ADR 0016).
/// </summary>
/// <remarks>
/// The read-side twin of <see cref="WritePortRegistration"/>. A read port never writes, so it does not take the
/// handler's <see cref="IDocumentSession"/>: its implementation takes only the context's store (the interface its
/// <c>.Postgresql</c> declares, deriving from <see cref="IDocumentStore"/>) and opens a short query session for each
/// read. It holds no state, so it is a singleton, and the handlers that take it get no transaction, no outbox and no
/// idempotency gate (that policy applies only to commands, ADR 0018).
/// </remarks>
public static class ReadPortRegistration
{
    /// <summary>Registers <typeparamref name="TImplementation"/> as the context's <typeparamref name="TPort"/>.</summary>
    /// <typeparam name="TPort">The read port, declared in the context's core.</typeparam>
    /// <typeparam name="TImplementation">
    /// Its Marten implementation, in the context's <c>.Postgresql</c>, with a public constructor that takes only the
    /// context's store.
    /// </typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TImplementation"/> has no public constructor that takes only a context's store.
    /// </exception>
    public static IHostApplicationBuilder AddReadPort<TPort, TImplementation>(this IHostApplicationBuilder builder)
        where TPort : class, IReadPort
        where TImplementation : class, TPort
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!TakesOnlyAContextStore(typeof(TImplementation)))
        {
            throw new ArgumentException(
                $"{typeof(TImplementation).Name} must have a public constructor that takes only its context's store (an interface deriving from IDocumentStore): a read port reads through query sessions and never writes (ADR 0016).",
                nameof(TImplementation));
        }

        builder.Services.AddSingleton<TPort, TImplementation>();
        builder.Services.ConfigureWolverine(options =>
        {
            // The generated handler code may name the port and its implementation, which live in the context's own
            // assemblies: Wolverine compiles against them only when told to.
            options.CodeGeneration.ReferenceAssembly(typeof(TPort).Assembly);
            options.CodeGeneration.ReferenceAssembly(typeof(TImplementation).Assembly);
        });
        return builder;
    }

    private static bool TakesOnlyAContextStore(Type implementation) =>
        implementation.GetConstructors().Any(constructor => constructor.GetParameters() switch
        {
            [var store] => store.ParameterType.IsInterface
                && store.ParameterType != typeof(IDocumentStore)
                && typeof(IDocumentStore).IsAssignableFrom(store.ParameterType),
            _ => false,
        });
}
