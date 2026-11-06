using JasperFx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

namespace RimBank.Postgresql;

/// <summary>
/// The host's own Wolverine message store (ADR 0017). Wolverine needs exactly one "main" store for its node, agent
/// and dead-letter bookkeeping, and refuses to start when only ancillary stores exist. The main store is not a
/// context's: it has its own schema <c>wolverine</c>, its own role <c>rimbank_wolverine</c> and its own connection
/// string <c>ConnectionStrings:Wolverine</c>, and no context role has any grant on it. Every context's outbox and
/// inbox stay in the context's own schema (<see cref="ServiceDomainStoreRegistration"/>).
/// </summary>
public static class MessagingNodeRegistration
{
    /// <summary>
    /// The host's message store, named like a service domain so it gets the same role, provisioning and health-check
    /// conventions (ADR 0016), although it is not one.
    /// </summary>
    public static readonly ServiceDomainName MessagingNode = ServiceDomainName.Parse("Wolverine");

    /// <summary>
    /// Persists Wolverine's own bookkeeping in the host's message store. Call it inside <c>UseWolverine</c>, where
    /// Wolverine still accepts service registrations; the composition root calls it once.
    /// </summary>
    /// <param name="options">The host's Wolverine options.</param>
    /// <param name="environment">The host environment: schema objects are created automatically everywhere but in Production.</param>
    /// <returns>The same options.</returns>
    public static WolverineOptions PersistMessagingNode(this WolverineOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        ServiceDomainStoreRegistration.AddDatabaseAccess(options.Services, MessagingNode);
        options.PersistMessagesWithPostgresqlUsingDataSourceFromContainer(
            provider => provider.GetRequiredKeyedService<NpgsqlDataSource>(MessagingNode.Value),
            MessagingNode.SchemaName,
            MessageStoreRole.Main);
        options.AutoBuildMessageStorageOnStartup = environment.IsProduction() ? AutoCreate.None : AutoCreate.CreateOrUpdate;
        return options;
    }
}
