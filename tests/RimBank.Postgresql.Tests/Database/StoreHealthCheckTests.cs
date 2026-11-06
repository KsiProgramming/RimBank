using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;

namespace RimBank.Postgresql.Database.Tests;

// Each store's ready check logs in as the context's own role (ADR 0016).
public sealed class StoreHealthCheckTests(PostgresqlServer server)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_context_is_healthy_when_its_role_reaches_its_database()
    {
        await using var host = await RunningHost.StartAsync(server);

        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Tags.Contains(ServiceDomainStoreRegistration.ReadyTag), Cancellation);

        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.Keys.ShouldBe(["postgresql-fake-ledger", "postgresql-fake-registry", "postgresql-wolverine"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_context_that_cannot_log_in_is_unhealthy_and_the_other_is_not()
    {
        var database = await server.CreateDatabaseAsync();
        var registryConnectionString = server.ContextConnectionString(database, FakeRegistry.ServiceDomain);
        await using (var administrator = NpgsqlDataSource.Create(server.AdministratorConnectionString(database)))
        {
            await PostgresqlServer.ExclusivelyAsync(() => ServiceDomainDatabase.ProvisionAsync(
                administrator, ServiceDomainName.Parse(FakeRegistry.ServiceDomain), registryConnectionString, Cancellation));
        }

        // The ledger logs in with a wrong password, and the host provisions nothing, so the role keeps its password.
        using var host = server.BuildHost(database, overrides: new Dictionary<string, string?>
        {
            ["RimBank:Postgresql:ProvisionRoles"] = "false",
            [$"ConnectionStrings:{FakeLedger.ServiceDomain}"] = server.ContextConnectionString(database, FakeLedger.ServiceDomain, password: "wrong-password"),
        });

        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Cancellation);

        report.Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries["postgresql-fake-ledger"].Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries["postgresql-fake-ledger"].Description.ShouldBe("The context cannot reach its database.");
        report.Entries["postgresql-fake-registry"].Status.ShouldBe(HealthStatus.Healthy);
    }
}
