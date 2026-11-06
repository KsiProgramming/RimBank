using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace RimBank.Postgresql;

/// <summary>
/// Provisions one context's role and schema when the host starts, before Marten creates its schema objects, when
/// <c>RimBank:Postgresql:ProvisionRoles</c> is on. It is on by default in Development only (ADR 0016).
/// </summary>
internal sealed class ServiceDomainRoleProvisioning(ServiceDomainName serviceDomain, IConfiguration configuration, IHostEnvironment environment)
    : IHostedLifecycleService
{
    /// <summary>The setting that turns provisioning on or off.</summary>
    public const string ProvisionRolesSetting = "RimBank:Postgresql:ProvisionRoles";

    /// <summary>The administrator connection string (the Aspire <c>rimbank</c> database), used only to provision.</summary>
    public const string AdministratorConnectionStringName = "rimbank";

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue(ProvisionRolesSetting, environment.IsDevelopment()))
        {
            return;
        }

        var administrator = configuration.GetConnectionString(AdministratorConnectionStringName) is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException(
                $"{ProvisionRolesSetting} is on, but the administrator connection string 'ConnectionStrings:{AdministratorConnectionStringName}' is missing.");

        await using var dataSource = NpgsqlDataSource.Create(administrator);
        await ServiceDomainDatabase.ProvisionAsync(
            dataSource,
            serviceDomain,
            ServiceDomainStoreRegistration.ContextConnectionString(configuration, serviceDomain),
            cancellationToken).ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
