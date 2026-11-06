using JasperFx;
using JasperFx.Events.Daemon;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace RimBank.Postgresql;

/// <summary>
/// The one way a context's <c>.Postgresql</c> adapter registers its Marten store (specs/tech-stack.md §2, ADR 0016).
/// </summary>
public static class ServiceDomainStoreRegistration
{
    /// <summary>The health check tag of every store's check: the context can reach its own database.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers the context's own Marten store as <typeparamref name="TStore"/>: its own schema, its own login role
    /// (from <c>ConnectionStrings:&lt;ServiceDomain&gt;</c>), its own async daemon, and a <c>ready</c> health check.
    /// </summary>
    /// <typeparam name="TStore">
    /// The context's store marker, declared in its <c>.Postgresql</c> adapter, for example
    /// <c>public interface IPartyReferenceDataDirectoryStore : IDocumentStore;</c>.
    /// </typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="serviceDomain">The BIAN service domain name in PascalCase.</param>
    /// <param name="configure">The context's own store options: its event types and its views.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="InvalidOperationException">A store is already registered for this service domain.</exception>
    public static IHostApplicationBuilder AddServiceDomainStore<TStore>(this IHostApplicationBuilder builder, string serviceDomain, Action<StoreOptions>? configure = null)
        where TStore : class, IDocumentStore
    {
        ArgumentNullException.ThrowIfNull(builder);
        var name = ServiceDomainName.Parse(serviceDomain);
        var services = builder.Services;

        if (services.Any(descriptor => descriptor.ServiceType == typeof(NpgsqlDataSource) && Equals(descriptor.ServiceKey, name.Value)))
        {
            throw new InvalidOperationException($"A Marten store is already registered for the service domain {name}. Each context registers exactly one store.");
        }

        // One data source per context, logged in as the context's own role. Marten and the health check share it.
        services.AddKeyedSingleton(name.Value, (provider, _) => NpgsqlDataSource.Create(ContextConnectionString(provider.GetRequiredService<IConfiguration>(), name)));

        // Registered before the store, so it runs before Marten creates the schema objects (ADR 0016).
        services.AddSingleton<IHostedService>(provider => new ServiceDomainRoleProvisioning(
            name,
            provider.GetRequiredService<IConfiguration>(),
            provider.GetRequiredService<IHostEnvironment>()));

        var production = builder.Environment.IsProduction();
        var store = services
            .AddMartenStore<TStore>(provider =>
            {
                var options = new StoreOptions();
                options.Connection(provider.GetRequiredKeyedService<NpgsqlDataSource>(name.Value));
                options.DatabaseSchemaName = name.SchemaName;
                options.Events.DatabaseSchemaName = name.SchemaName;
                options.AutoCreateSchemaObjects = production ? AutoCreate.None : AutoCreate.CreateOrUpdate;
                configure?.Invoke(options);
                return options;
            })
            .AddAsyncDaemon(DaemonMode.HotCold);
        if (!production)
        {
            store.ApplyAllDatabaseChangesOnStartup();
        }

        services.AddHealthChecks().Add(new HealthCheckRegistration(
            name.HealthCheckName,
            provider => new ServiceDomainStoreHealthCheck(provider.GetRequiredKeyedService<NpgsqlDataSource>(name.Value)),
            failureStatus: null,
            tags: [ReadyTag]));

        return builder;
    }

    internal static string ContextConnectionString(IConfiguration configuration, ServiceDomainName name) =>
        configuration.GetConnectionString(name.ConnectionStringName) is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException(
                $"The connection string 'ConnectionStrings:{name.ConnectionStringName}' is missing. It logs in as the context's own role '{name.RoleName}' (ADR 0016).");
}
