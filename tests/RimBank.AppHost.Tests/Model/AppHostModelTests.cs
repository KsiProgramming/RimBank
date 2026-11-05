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
    public async Task Host_waits_for_the_database_and_is_probed_on_health()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(TestContext.Current.CancellationToken);
        var host = builder.CreateResourceBuilder<ProjectResource>(Program.HostResource).Resource;

        host.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => wait.Resource.Name == Program.DatabaseResource);
        host.Annotations.OfType<HealthCheckAnnotation>().ShouldNotBeEmpty();
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
