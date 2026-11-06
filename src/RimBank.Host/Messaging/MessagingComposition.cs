using JasperFx.CodeGeneration;
using RimBank.Postgresql;
using Wolverine;

namespace RimBank.Host.Messaging;

/// <summary>
/// The host's single Wolverine bus (specs/tech-stack.md §1, ADR 0017). Every context's <c>.Api</c>, <c>.Mcp</c> and
/// <c>.Subscriptions</c> dispatch through it; each context's <c>.Postgresql</c> adds its own handlers, store binding
/// and durable local queue (<see cref="ServiceDomainStoreRegistration"/>).
/// </summary>
public static class MessagingComposition
{
    /// <summary>
    /// Adds Wolverine: no handler discovery outside the contexts' own assemblies, transactions for every handler that
    /// writes through a store, and <see cref="DurabilityMode.Balanced"/> so several host instances share the work.
    /// When <c>ConnectionStrings:Wolverine</c> is set, Wolverine keeps its node bookkeeping in the host's own message
    /// store; without it (no context yet), messages stay in memory.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddRimBankMessaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.UseWolverine(options =>
        {
            // The host holds no handler: a handler is found only because its context includes its own assembly.
            options.ApplicationAssembly = typeof(MessagingComposition).Assembly;
            options.Durability.Mode = DurabilityMode.Balanced;
            options.Policies.AutoApplyTransactions();

            // Handler code is generated with Roslyn at startup; pre-generated code is decided with the container
            // images (roadmap 7.5, ADR 0017).
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
            options.UseRuntimeCompilation();

            if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString(MessagingNodeRegistration.MessagingNode.ConnectionStringName)))
            {
                options.PersistMessagingNode(builder.Environment);
            }
        });

        return builder;
    }
}
