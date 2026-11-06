using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RimBank.Postgresql.Database.Tests;
using RimBank.Postgresql.FakeContexts.Tests;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresqlServer))]

namespace RimBank.Postgresql.Database.Tests;

/// <summary>
/// One PostgreSQL container for the whole test assembly. Each test gets its own database, so tests never share
/// schemas, views or daemons; the context roles are server-wide, so they share one generated password.
/// </summary>
public sealed class PostgresqlServer : IAsyncLifetime
{
    // Roles are shared by every database on the server, and two hosts provisioning the same role at the same
    // moment would race on ALTER ROLE. Hosts in this assembly start one at a time.
    private static readonly SemaphoreSlim _startup = new(1, 1);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>The password of every fake context role, generated per test run and never written anywhere.</summary>
    public string RolePassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates an empty database and returns its name.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var database = "test_" + Guid.CreateVersion7().ToString("N");
        await using var dataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
        await using var command = dataSource.CreateCommand($"CREATE DATABASE {database}");
        await command.ExecuteNonQueryAsync();
        return database;
    }

    /// <summary>The administrator's connection string to <paramref name="database"/>.</summary>
    public string AdministratorConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    /// <summary>The connection string of a context's own role to <paramref name="database"/>.</summary>
    public string ContextConnectionString(string database, string serviceDomain, string? password = null) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
            Username = ServiceDomainName.Parse(serviceDomain).RoleName,
            Password = password ?? RolePassword,
        }.ConnectionString;

    /// <summary>
    /// Builds a host with both fake contexts' stores on <paramref name="database"/>, provisioning their roles, as a
    /// real host composes its contexts' <c>.Postgresql</c> adapters.
    /// </summary>
    public IHost BuildHost(string database, string environment = "Development", IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:rimbank"] = AdministratorConnectionString(database),
            [$"ConnectionStrings:{FakeLedger.ServiceDomain}"] = ContextConnectionString(database, FakeLedger.ServiceDomain),
            [$"ConnectionStrings:{FakeRegistry.ServiceDomain}"] = ContextConnectionString(database, FakeRegistry.ServiceDomain),
            ["RimBank:Postgresql:ProvisionRoles"] = "true",
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddServiceDomainStore<IFakeLedgerStore>(FakeLedger.ServiceDomain, FakeLedger.Configure);
        builder.AddServiceDomainStore<IFakeRegistryStore>(FakeRegistry.ServiceDomain, FakeRegistry.Configure);
        return builder.Build();
    }

    /// <summary>Builds and starts a host on a new database.</summary>
    public async Task<IHost> StartHostAsync(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var host = BuildHost(await CreateDatabaseAsync(), overrides: overrides);
        await StartAsync(host);
        return host;
    }

    /// <summary>Starts a host, one at a time (see <see cref="_startup"/>).</summary>
    public static Task StartAsync(IHost host) => ExclusivelyAsync(() => host.StartAsync(TestContext.Current.CancellationToken));

    /// <summary>Runs something that provisions roles, one at a time (see <see cref="_startup"/>).</summary>
    public static async Task ExclusivelyAsync(Func<Task> provisioning)
    {
        ArgumentNullException.ThrowIfNull(provisioning);
        await _startup.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await provisioning();
        }
        finally
        {
            _startup.Release();
        }
    }
}
