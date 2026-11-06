using Npgsql;

namespace RimBank.Postgresql;

/// <summary>
/// Creates a context's login role and schema (ADR 0016). The role owns its schema and is given nothing on any other
/// schema, so one context can never read another context's events or views (specs/tech-stack.md §2, §8.4).
/// </summary>
/// <remarks>
/// It needs an administrator connection, so it runs only in Development and tests. In production the roles and
/// schemas are created outside the application, and RimBank never connects with administrator rights.
/// </remarks>
public static class ServiceDomainDatabase
{
    // The password travels only as a bind parameter, through a transaction-local setting, so it never appears in a
    // statement text that Npgsql or PostgreSQL could log.
    private const string PasswordSetting = "rimbank.provisioned_role_password";

    /// <summary>Creates or updates the context's role and schema. Running it again changes nothing but the password.</summary>
    /// <param name="administrator">A data source logged in as a role that may create roles and schemas.</param>
    /// <param name="serviceDomain">The context whose role and schema to create.</param>
    /// <param name="contextConnectionString">
    /// The context's own connection string. Its user must be the context's role, and its password becomes the role's password.
    /// </param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when the role and schema exist.</returns>
    /// <exception cref="ArgumentException">The connection string logs in as another role, or has no password.</exception>
    public static async Task ProvisionAsync(
        NpgsqlDataSource administrator,
        ServiceDomainName serviceDomain,
        string contextConnectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        ArgumentNullException.ThrowIfNull(serviceDomain);
        var login = new NpgsqlConnectionStringBuilder(contextConnectionString);
        if (!string.Equals(login.Username, serviceDomain.RoleName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The connection string of {serviceDomain} must log in as its own role '{serviceDomain.RoleName}', not '{login.Username}'.",
                nameof(contextConnectionString));
        }

        if (string.IsNullOrEmpty(login.Password))
        {
            throw new ArgumentException($"The connection string of {serviceDomain} has no password.", nameof(contextConnectionString));
        }

        await using var connection = await administrator.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Role and schema names come from a validated ServiceDomainName (ASCII letters and underscores) and are
        // still quoted with format('%I').
        await using (var setPassword = new NpgsqlCommand($"SELECT set_config('{PasswordSetting}', $1, true)", connection, transaction))
        {
            setPassword.Parameters.Add(new NpgsqlParameter { Value = login.Password });
            await setPassword.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var role = serviceDomain.RoleName;
        var schema = serviceDomain.SchemaName;
        var provision = $"""
            DO $provision$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{role}') THEN
                    EXECUTE format('CREATE ROLE %I', '{role}');
                END IF;
                EXECUTE format(
                    'ALTER ROLE %I WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD %L',
                    '{role}',
                    current_setting('{PasswordSetting}'));
                EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION %I', '{schema}', '{role}');
                EXECUTE format('ALTER SCHEMA %I OWNER TO %I', '{schema}', '{role}');
            END
            $provision$
            """;
        await using (var command = new NpgsqlCommand(provision, connection, transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
