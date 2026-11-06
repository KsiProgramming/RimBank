using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace RimBank.Postgresql;

/// <summary>
/// Checks that a context reaches its database as its own role (ADR 0016). The result holds no connection details;
/// the health endpoints print the status only (ADR 0014).
/// </summary>
internal sealed class ServiceDomainStoreHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The context cannot reach its database.", exception);
        }
    }
}
