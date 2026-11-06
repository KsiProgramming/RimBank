using Npgsql;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;

namespace RimBank.Postgresql.Database.Tests;

// tech-stack §2 and §8.4: each context logs in as its own role, which owns its own schema and is given nothing on any
// other schema, so a context can never read another context's events, views, outbox or inbox (ADR 0016), nor the
// host's own Wolverine schema (ADR 0017). PostgreSQL enforces it.
public sealed class RoleIsolationTests(PostgresqlServer server)
{
    private const string InsufficientPrivilege = "42501";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("SELECT count(*) FROM fake_registry.mt_events")]
    [InlineData("SELECT count(*) FROM fake_registry.mt_doc_fakeregistryentryview")]
    [InlineData("SELECT count(*) FROM fake_registry.wolverine_incoming_envelopes")]
    [InlineData("SELECT count(*) FROM fake_registry.wolverine_outgoing_envelopes")]
    [InlineData("SELECT count(*) FROM wolverine.wolverine_nodes")]
    [InlineData("SELECT count(*) FROM wolverine.wolverine_incoming_envelopes")]
    [InlineData("CREATE TABLE fake_registry.intruder (id int)")]
    public async Task A_context_role_cannot_read_or_write_another_contexts_schema(string sql)
    {
        await using var host = await RunningHost.StartAsync(server);
        await using var ledgerRole = NpgsqlDataSource.Create(server.ContextConnectionString(host.Database, FakeLedger.ServiceDomain));
        await using var command = ledgerRole.CreateCommand(sql);

        var exception = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Cancellation));

        exception.SqlState.ShouldBe(InsufficientPrivilege);
    }

    [Fact]
    public async Task Each_schema_is_owned_by_its_own_context_role()
    {
        await using var host = await RunningHost.StartAsync(server);
        await using var administrator = NpgsqlDataSource.Create(server.AdministratorConnectionString(host.Database));
        await using var command = administrator.CreateCommand(
            "SELECT nspname, pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname LIKE 'fake\\_%' OR nspname = 'wolverine' ORDER BY nspname");
        await using var reader = await command.ExecuteReaderAsync(Cancellation);

        var owners = new List<(string Schema, string Owner)>();
        while (await reader.ReadAsync(Cancellation))
        {
            owners.Add((reader.GetString(0), reader.GetString(1)));
        }

        owners.ShouldBe([("fake_ledger", "rimbank_fake_ledger"), ("fake_registry", "rimbank_fake_registry"), ("wolverine", "rimbank_wolverine")]);
    }

    [Fact]
    public async Task Provisioning_twice_changes_nothing()
    {
        var database = await server.CreateDatabaseAsync();
        await using var administrator = NpgsqlDataSource.Create(server.AdministratorConnectionString(database));
        var ledger = ServiceDomainName.Parse(FakeLedger.ServiceDomain);
        var connectionString = server.ContextConnectionString(database, FakeLedger.ServiceDomain);

        await PostgresqlServer.ExclusivelyAsync(() => ServiceDomainDatabase.ProvisionAsync(administrator, ledger, connectionString, Cancellation));
        await PostgresqlServer.ExclusivelyAsync(() => ServiceDomainDatabase.ProvisionAsync(administrator, ledger, connectionString, Cancellation));

        await using var ledgerRole = NpgsqlDataSource.Create(connectionString);
        await using var command = ledgerRole.CreateCommand("CREATE TABLE fake_ledger.own_table (id int)");
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    [Fact]
    public async Task Provisioning_refuses_a_connection_string_that_logs_in_as_another_role()
    {
        var database = await server.CreateDatabaseAsync();
        await using var administrator = NpgsqlDataSource.Create(server.AdministratorConnectionString(database));

        var exception = await Should.ThrowAsync<ArgumentException>(() => ServiceDomainDatabase.ProvisionAsync(
            administrator,
            ServiceDomainName.Parse(FakeLedger.ServiceDomain),
            server.ContextConnectionString(database, FakeRegistry.ServiceDomain),
            Cancellation));

        exception.Message.ShouldContain("rimbank_fake_ledger");
    }
}
