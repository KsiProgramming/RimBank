using System.Security.Cryptography;
using Alba;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RimBank.Host;
using RimBank.Host.Testing;
using RimBank.Postgresql;
using RimBank.SharedKernel.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(RimBank.Party.PartyReferenceDataDirectory.Api.Tests.PostgresqlServer))]

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

/// <summary>
/// One PostgreSQL container for the whole test assembly. Each test gets its own database and its own
/// <c>RimBank.Host</c>, started one at a time because the context's role is shared by every database on the server
/// (ADR 0016).
/// </summary>
public sealed class PostgresqlServer : IAsyncLifetime
{
    /// <summary>The time every test host runs at: the open date of every registered party.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private const string ServiceDomain = "PartyReferenceDataDirectory";

    private static readonly SemaphoreSlim _startup = new(1, 1);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>The password of the context roles, generated per test run and never written anywhere.</summary>
    private string RolePassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Starts <c>RimBank.Host</c> exactly as <c>Program</c> composes it, in <paramref name="environment"/>, on a new
    /// database, trusting <see cref="TestTokens"/> and running at <see cref="Now"/>. The default <c>Test</c> environment
    /// behaves like Production (HTTPS authority, no request-binding exceptions) except that the stores create their
    /// schema, which Production never does (ADR 0016). With <paramref name="logs"/>, every log entry from Debug up is also
    /// captured there.
    /// </summary>
    public async Task<PartyApiHost> StartHostAsync(string environment = "Test", CapturedLogs? logs = null)
    {
        var database = await CreateDatabaseAsync();
        await _startup.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            var host = await AlbaHost.For<Host.Program>(web =>
            {
                web.UseEnvironment(environment);
                web.UseSetting("ConnectionStrings:rimbank", AdministratorConnectionString(database));
                web.UseSetting($"ConnectionStrings:{ServiceDomain}", RoleConnectionString(database, ServiceDomain));
                web.UseSetting($"ConnectionStrings:{MessagingNodeRegistration.MessagingNode.ConnectionStringName}", RoleConnectionString(database, MessagingNodeRegistration.MessagingNode.Value));
                web.UseSetting("RimBank:Postgresql:ProvisionRoles", "true");
                web.UseSetting(AuthenticationComposition.AuthorityKey, TestTokens.Issuer);
                if (logs is not null)
                {
                    web.UseSetting("Logging:LogLevel:Default", "Debug");
                }

                web.ConfigureServices(services =>
                {
                    services.TrustTestTokens();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                    if (logs is not null)
                    {
                        services.AddSingleton<ILoggerProvider>(logs);
                    }
                });
            });

            return new PartyApiHost(host, NpgsqlDataSource.Create(AdministratorConnectionString(database)));
        }
        finally
        {
            _startup.Release();
        }
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
