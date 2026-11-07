using JasperFx;
using JasperFx.Events.Daemon;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Weasel.Core;
using Wolverine;
using Wolverine.Marten;

namespace RimBank.Postgresql;

/// <summary>
/// The one way a context's <c>.Postgresql</c> adapter registers its Marten store (specs/tech-stack.md §2, ADR 0016),
/// connected to Wolverine (ADR 0017).
/// </summary>
public static class ServiceDomainStoreRegistration
{
    /// <summary>The health check tag of every store's check: the context can reach its own database.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers the context's own Marten store as <typeparamref name="TStore"/>: its own schema, its own login role
    /// (from <c>ConnectionStrings:&lt;ServiceDomain&gt;</c>), its own async daemon, and a <c>ready</c> health check
    /// (ADR 0016). It also connects the store to Wolverine (ADR 0017): the outbox and inbox tables live in the context's
    /// own schema, every handler in the context's namespace commits through this store, the context's own messages
    /// go to its durable local queue, and every command is handled once per idempotency key (ADR 0018).
    /// </summary>
    /// <typeparam name="TStore">
    /// The context's store marker, declared in its <c>.Postgresql</c> adapter, for example
    /// <c>public interface IPartyReferenceDataDirectoryStore : IDocumentStore;</c>.
    /// </typeparam>
    /// <typeparam name="TCore">
    /// Any type in the root namespace of the context's core, for example its Control Record. Its assembly
    /// (<c>RimBank.Core</c>, shared by every context, ADR 0023) is searched for handlers, and its namespace (with child
    /// namespaces, so the context's own adapters too) is bound to the store: the namespace, not the assembly, tells
    /// contexts apart.
    /// </typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="serviceDomain">The BIAN service domain name in PascalCase.</param>
    /// <param name="configure">The context's own store options: its event types and its views.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="InvalidOperationException">A store is already registered for this service domain.</exception>
    public static IHostApplicationBuilder AddServiceDomainStore<TStore, TCore>(this IHostApplicationBuilder builder, string serviceDomain, Action<StoreOptions>? configure = null)
        where TStore : class, IDocumentStore
    {
        ArgumentNullException.ThrowIfNull(builder);
        var name = ServiceDomainName.Parse(serviceDomain);
        if (name == MessagingNodeRegistration.MessagingNode)
        {
            throw new ArgumentException($"'{name}' is reserved for the host's own message store.", nameof(serviceDomain));
        }

        var coreNamespace = typeof(TCore).Namespace
            ?? throw new ArgumentException($"The core type {typeof(TCore).Name} of {name} must be in a namespace.", nameof(TCore));
        var services = builder.Services;
        if (IsRegistered(services, name))
        {
            throw new InvalidOperationException($"A Marten store is already registered for the service domain {name}. Each context registers exactly one store.");
        }

        var production = builder.Environment.IsProduction();
        AddDatabaseAccess(services, name);

        var store = services
            .AddMartenStore<TStore>(provider =>
            {
                var options = new StoreOptions();
                options.Connection(provider.GetRequiredKeyedService<NpgsqlDataSource>(name.Value));
                options.DatabaseSchemaName = name.SchemaName;
                options.Events.DatabaseSchemaName = name.SchemaName;
                options.AutoCreateSchemaObjects = production ? AutoCreate.None : AutoCreate.CreateOrUpdate;

                // Typed ids as plain GUID strings and enums as names, in events and documents alike (ADR 0016).
                options.UseSystemTextJsonForSerialization(EnumStorage.AsString, configure: StoredJson.AddConverters);

                // The outcomes of the context's commands, next to its events (ADR 0018).
                options.Schema.For<IdempotentOutcome>();
                configure?.Invoke(options);
                return options;
            })

            // The outbox and inbox tables live next to the events, in the context's own schema, owned by its own role.
            .IntegrateWithWolverine(integration => integration.SchemaName = name.SchemaName)
            .AddAsyncDaemon(DaemonMode.HotCold);
        if (!production)
        {
            store.ApplyAllDatabaseChangesOnStartup();
        }

        services.ConfigureWolverine(options =>
        {
            options.Discovery.IncludeAssembly(typeof(TCore).Assembly);

            // The generated handler code names the store marker, declared in the context's own .Postgresql assembly.
            options.CodeGeneration.ReferenceAssembly(typeof(TStore).Assembly);

            // Every handler in the context's namespace commits through the context's own store, so the core needs no
            // Wolverine attribute and a handler cannot commit to another context's store by mistake.
            options.Policies.UseAncillaryStorageFromNamespace(typeof(TStore), coreNamespace);

            // Every command of the context is handled once per idempotency key and caller, in the same transaction
            // (ADR 0018, ADR 0019). The caller is the envelope's user name, which Wolverine relays only when asked to.
            options.EnableRelayOfUserName = true;
            options.Policies.Add(new CommandIdempotencyPolicy(coreNamespace, name.SchemaName));

            // A command whose commit loses an optimistic-concurrency race runs once more, so its handler's own version
            // check answers the conflict as a value (ADR 0027).
            options.Policies.Add(new CommandConcurrencyRetryPolicy(coreNamespace));

            // The context's own messages go to its own durable local queue: they survive a restart and are retried,
            // and a backlog in one context never delays another.
            options.Publish(rule =>
            {
                rule.MessagesFromNamespace(coreNamespace);
                rule.ToLocalQueue(name.QueueName);
            });
            options.LocalQueue(name.QueueName).UseDurableInbox();
        });

        return builder;
    }

    internal static string ContextConnectionString(IConfiguration configuration, ServiceDomainName name) =>
        configuration.GetConnectionString(name.ConnectionStringName) is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException(
                $"The connection string 'ConnectionStrings:{name.ConnectionStringName}' is missing. It logs in as the context's own role '{name.RoleName}' (ADR 0016).");

    private static bool IsRegistered(IServiceCollection services, ServiceDomainName name) =>
        services.Any(descriptor => descriptor.ServiceType == typeof(NpgsqlDataSource) && Equals(descriptor.ServiceKey, name.Value));

    // One data source per role, the role and schema provisioner (Development and tests only), and a ready health check.
    internal static void AddDatabaseAccess(IServiceCollection services, ServiceDomainName name)
    {
        services.AddKeyedSingleton(name.Value, (provider, _) => NpgsqlDataSource.Create(ContextConnectionString(provider.GetRequiredService<IConfiguration>(), name)));

        // Registered before the store, so it runs before Marten and Wolverine create their schema objects (ADR 0016).
        services.AddSingleton<IHostedService>(provider => new ServiceDomainRoleProvisioning(
            name,
            provider.GetRequiredService<IConfiguration>(),
            provider.GetRequiredService<IHostEnvironment>()));

        services.AddHealthChecks().Add(new HealthCheckRegistration(
            name.HealthCheckName,
            provider => new ServiceDomainStoreHealthCheck(provider.GetRequiredKeyedService<NpgsqlDataSource>(name.Value)),
            failureStatus: null,
            tags: [ReadyTag]));
    }
}
