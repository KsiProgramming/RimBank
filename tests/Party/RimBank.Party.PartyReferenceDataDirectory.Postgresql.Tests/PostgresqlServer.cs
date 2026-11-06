using System.Security.Cryptography;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;
using RimBank.Postgresql;
using RimBank.SharedKernel.Testing;
using Testcontainers.PostgreSql;
using Wolverine;

[assembly: AssemblyFixture(typeof(PostgresqlServer))]

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

/// <summary>
/// One PostgreSQL container for the whole test assembly. Each test gets its own database, and hosts start one at a
/// time because the context's role is shared by every database on the server (ADR 0016).
/// </summary>
public sealed class PostgresqlServer : IAsyncLifetime
{
    /// <summary>The time every test host runs at: the handler's open date and the ids' timestamp.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private static readonly SemaphoreSlim _startup = new(1, 1);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>The password of the context's role, generated per test run and never written anywhere.</summary>
    private string RolePassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Builds and starts a host with the Party store on a new database, as <c>RimBank.Host</c> composes it.</summary>
    public async Task<PartyHost> StartHostAsync()
    {
        var database = await CreateDatabaseAsync();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development", DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:rimbank"] = AdministratorConnectionString(database),
            [$"ConnectionStrings:{PartyReferenceDataDirectoryPostgresql.ServiceDomain}"] = RoleConnectionString(database, PartyReferenceDataDirectoryPostgresql.ServiceDomain),
            [$"ConnectionStrings:{MessagingNodeRegistration.MessagingNode}"] = RoleConnectionString(database, MessagingNodeRegistration.MessagingNode.Value),
            ["RimBank:Postgresql:ProvisionRoles"] = "true",
        });
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        builder.UseWolverine(options =>
        {
            // As RimBank.Host sets up its bus (ADR 0017), except that each test host runs alone on its database.
            options.ApplicationAssembly = typeof(ServiceDomainName).Assembly;
            options.Durability.Mode = DurabilityMode.Solo;
            options.Policies.AutoApplyTransactions();
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
            options.UseRuntimeCompilation();
            options.PersistMessagingNode(builder.Environment);
        });
        builder.AddPartyReferenceDataDirectoryPostgresql();

        var host = builder.Build();
        await _startup.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            _startup.Release();
        }

        return new PartyHost(host, NpgsqlDataSource.Create(AdministratorConnectionString(database)));
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var database = "test_" + Guid.CreateVersion7().ToString("N");
        await using var dataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
        await using var command = dataSource.CreateCommand($"CREATE DATABASE {database}");
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return database;
    }

    private string AdministratorConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    private string RoleConnectionString(string database, string serviceDomain) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
            Username = ServiceDomainName.Parse(serviceDomain).RoleName,
            Password = RolePassword,
        }.ConnectionString;
}
