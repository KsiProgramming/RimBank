namespace RimBank.AppHost;

/// <summary>
/// The Aspire AppHost: one PostgreSQL server with the <c>rimbank</c> database, and <c>RimBank.Host</c>, which waits
/// for the database (ADR 0014). Each context gets its own schema and role inside that database in roadmap 0.10.
/// </summary>
public sealed class Program
{
    /// <summary>The PostgreSQL server resource.</summary>
    public const string PostgresResource = "postgres";

    /// <summary>The RimBank database resource; also the connection string name the host receives.</summary>
    public const string DatabaseResource = "rimbank";

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

        builder.AddProject<Projects.RimBank_Host>(HostResource)
            .WithReference(database)
            .WaitFor(database)
            .WithHttpHealthCheck("/health");

        builder.Build().Run();
    }
}
