namespace RimBank.AppHost;

/// <summary>
/// The Aspire AppHost: one PostgreSQL server with the <c>rimbank</c> database, a Keycloak server with the dev-only
/// <c>rimbank</c> realm (ADR 0019), and <c>RimBank.Host</c>, which waits for both (ADR 0014) and tells MCP clients
/// where Keycloak is (ADR 0022). Each context gets its own schema, role and connection string inside that database
/// (ADR 0016), and the host's message store gets the <c>Wolverine</c> one (ADR 0017).
/// </summary>
public sealed class Program
{
    /// <summary>
    /// The connection strings the host logs in with, one per role: the message store's and each context's. The host
    /// provisions each role from its own connection string in Development (ADR 0016).
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string Role)> RoleConnectionStrings =
    [
        ("Wolverine", "rimbank_wolverine"),
        ("PartyReferenceDataDirectory", "rimbank_party_reference_data_directory"),
    ];

    /// <summary>The PostgreSQL server resource.</summary>
    public const string PostgresResource = "postgres";

    /// <summary>The RimBank database resource; also the connection string name the host receives.</summary>
    public const string DatabaseResource = "rimbank";

    /// <summary>The Keycloak server resource; also the service name the host discovers it by (ADR 0019).</summary>
    public const string KeycloakResource = "keycloak";

    /// <summary>The realm file Keycloak imports on its first start: local development only (ADR 0019).</summary>
    public const string RealmFile = "./Realms/rimbank-realm.json";

    /// <summary>The realm the realm file creates, which issues RimBank's access tokens (ADR 0019).</summary>
    public const string Realm = "rimbank";

    /// <summary>
    /// The variable that tells the host which Keycloak address MCP clients sign in to: the realm on Keycloak's own
    /// endpoint, which a client outside the AppHost can reach, unlike the service-discovery name (ADR 0022).
    /// </summary>
    public const string McpAuthorizationServerVariable = "Mcp__AuthorizationServer";

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

        var host = builder.AddProject<Projects.RimBank_Host>(HostResource);
        foreach (var (name, role) in RoleConnectionStrings)
        {
            host.WithReference(AddRoleConnectionString(builder, postgres, name, role));
        }

        host.WithReference(database)
            .WithReference(keycloak)
            .WaitFor(database)
            .WaitFor(keycloak)
            .WithEnvironment(McpAuthorizationServerVariable, ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/{Realm}"))
            .WithHttpHealthCheck("/health");

        builder.Build().Run();
    }

    // A login as one role on the rimbank database. Its password is generated once and kept in user secrets, never in
    // source; it has no special characters, so it never breaks the connection string's syntax.
    private static IResourceBuilder<IResourceWithConnectionString> AddRoleConnectionString(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<PostgresServerResource> postgres,
        string name,
        string role)
    {
        var password = builder.AddParameter(
            $"{name.ToLowerInvariant()}-password",
            new GenerateParameterDefault { MinLength = 32, Special = false },
            secret: true,
            persist: true);
        var endpoint = postgres.Resource.PrimaryEndpoint;
        return builder.AddConnectionString(name, ReferenceExpression.Create(
            $"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database={DatabaseResource};Username={role};Password={password.Resource}"));
    }
}
