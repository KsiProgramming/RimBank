using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace RimBank.AppHost.Model.Tests;

// The AppHost's resource model, checked without starting any container (no Docker needed).
public sealed class AppHostModelTests
{
    [Fact]
    public async Task Host_receives_the_rimbank_connection_string()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(ct);
        var host = builder.CreateResourceBuilder<ProjectResource>(Program.HostResource);

        var configuration = await ExecutionConfigurationBuilder.Create(host.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new(DistributedApplicationOperation.Publish), NullLogger.Instance, ct);

        configuration.EnvironmentVariables.ToDictionary().ShouldContainKey($"ConnectionStrings__{Program.DatabaseResource}");
    }

    [Fact]
    public async Task Host_receives_a_role_connection_string_for_its_message_store_and_each_context()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(ct);
        var host = builder.CreateResourceBuilder<ProjectResource>(Program.HostResource);

        var configuration = await ExecutionConfigurationBuilder.Create(host.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new(DistributedApplicationOperation.Publish), NullLogger.Instance, ct);

        Program.RoleConnectionStrings.Select(login => login.Name).ShouldBe(["Wolverine", "PartyReferenceDataDirectory"]);
        var variables = configuration.EnvironmentVariables.ToDictionary();
        foreach (var (name, _) in Program.RoleConnectionStrings)
        {
            variables.ShouldContainKey($"ConnectionStrings__{name}");
        }
    }

    [Fact]
    public async Task Host_waits_for_the_database_and_is_probed_on_health()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(TestContext.Current.CancellationToken);
        var host = builder.CreateResourceBuilder<ProjectResource>(Program.HostResource).Resource;

        host.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => wait.Resource.Name == Program.DatabaseResource);
        host.Annotations.OfType<HealthCheckAnnotation>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Host_finds_keycloak_through_service_discovery_and_waits_for_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(ct);
        var host = builder.CreateResourceBuilder<ProjectResource>(Program.HostResource).Resource;

        var configuration = await ExecutionConfigurationBuilder.Create(host)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new(DistributedApplicationOperation.Publish), NullLogger.Instance, ct);

        host.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => wait.Resource.Name == Program.KeycloakResource);
        configuration.EnvironmentVariables.ToDictionary().Keys.ShouldContain(key => key.StartsWith($"services__{Program.KeycloakResource}__", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Host_tells_mcp_clients_the_keycloak_realm_they_can_reach()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(ct);
        var host = builder.CreateResourceBuilder<ProjectResource>(Program.HostResource).Resource;

        var configuration = await ExecutionConfigurationBuilder.Create(host)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new(DistributedApplicationOperation.Publish), NullLogger.Instance, ct);

        var variables = configuration.EnvironmentVariables.ToDictionary();
        variables.ShouldContainKey(Program.McpAuthorizationServerVariable);
        variables[Program.McpAuthorizationServerVariable].ShouldEndWith($"/realms/{Program.Realm}");
    }

    [Fact]
    public async Task Keycloak_imports_the_dev_realm_and_keeps_its_data_in_a_named_volume()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(TestContext.Current.CancellationToken);
        var keycloak = builder.CreateResourceBuilder<KeycloakResource>(Program.KeycloakResource).Resource;

        keycloak.Annotations.OfType<ContainerMountAnnotation>()
            .ShouldContain(mount => mount.Source == Program.KeycloakVolume && mount.Type == ContainerMountType.Volume);
        File.Exists(Path.Combine(builder.AppHostDirectory, Program.RealmFile)).ShouldBeTrue();
    }

    [Fact]
    public async Task Postgres_keeps_its_data_in_a_named_volume()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(TestContext.Current.CancellationToken);
        var postgres = builder.CreateResourceBuilder<PostgresServerResource>(Program.PostgresResource).Resource;

        postgres.Annotations.OfType<ContainerMountAnnotation>()
            .ShouldContain(mount => mount.Source == "rimbank-postgres-data" && mount.Type == ContainerMountType.Volume);
    }
}
