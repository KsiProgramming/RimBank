namespace RimBank.AppHost;

/// <summary>
/// The Aspire AppHost: one PostgreSQL server with the <c>rimbank</c> database, a Keycloak server with the dev-only
/// <c>rimbank</c> realm (ADR 0019), and <c>RimBank.Host</c>, which waits for both (ADR 0014). Each context gets its own schema, role and connection string inside that database
/// (ADR 0016); the first context adds its connection string here in roadmap 1.4.
/// </summary>
public sealed class Program
{
    /// <summary>The PostgreSQL server resource.</summary>
    public const string PostgresResource = "postgres";

    /// <summary>The RimBank database resource; also the connection string name the host receives.</summary>
    public const string DatabaseResource = "rimbank";

    /// <summary>The Keycloak server resource; also the service name the host discovers it by (ADR 0019).</summary>
    public const string KeycloakResource = "keycloak";

    /// <summary>The realm file Keycloak imports on its first start: local development only (ADR 0019).</summary>
    public const string RealmFile = "./Realms/rimbank-realm.json";

    /// <summary>The volume Keycloak keeps its realms in.</summary>
    public const string KeycloakVolume = "rimbank-keycloak-data";

    /// <summary>The <c>RimBank.Host</c> project resource.</summary>
    public const string HostResource = "rimbank-host";

    private Program()
    {
    }

    /// <summary>Builds and runs the distributed application.</summary>
    /// <param name="args">The command-line arguments.</param>
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        // The password is an Aspire-generated parameter kept in user secrets, never in source.
        var postgres = builder.AddPostgres(PostgresResource)
            .WithDataVolume("rimbank-postgres-data");
        var database = postgres.AddDatabase(DatabaseResource);

        // The admin password is also an Aspire-generated parameter. The realm file is for local development only:
        // its client secret is a published, dev-only value, never used in a deployed environment.
        var keycloak = builder.AddKeycloak(KeycloakResource)
            .WithDataVolume(KeycloakVolume)
            .WithRealmImport(RealmFile);

        builder.AddProject<Projects.RimBank_Host>(HostResource)
            .WithReference(database)
            .WithReference(keycloak)
            .WaitFor(database)
            .WaitFor(keycloak)
            .WithHttpHealthCheck("/health");

        builder.Build().Run();
    }
}
