using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace RimBank.AppHost.Startup.Tests;

// Starts the real AppHost (PostgreSQL and Keycloak containers + RimBank.Host) and probes the host. Needs Docker: it always runs in
// CI, and is skipped locally only when Docker is not reachable.
public sealed class AppHostStartupTests
{
    private static readonly TimeSpan _startupTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task Postgres_and_the_host_become_healthy_and_the_host_answers_its_probe()
    {
        DockerAvailability.SkipLocallyWhenUnavailable();
        var ct = TestContext.Current.CancellationToken;

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Program>(ct);
        WithoutDataVolume(builder);
        await using var app = await builder.BuildAsync(ct).WaitAsync(_startupTimeout, ct);
        await app.StartAsync(ct).WaitAsync(_startupTimeout, ct);

        await app.ResourceNotifications.WaitForResourceHealthyAsync(Program.PostgresResource, ct).WaitAsync(_startupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(Program.KeycloakResource, ct).WaitAsync(_startupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(Program.HostResource, ct).WaitAsync(_startupTimeout, ct);
        (await app.GetConnectionStringAsync(Program.DatabaseResource, ct)).ShouldNotBeNullOrWhiteSpace();

        using var client = app.CreateHttpClient(Program.HostResource, "http");
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct)).ShouldBe("Healthy");
    }

    // The test runs on throwaway containers: it never reads or writes a developer's data volumes, and a volume
    // initialised with another password cannot make it fail.
    private static void WithoutDataVolume(IDistributedApplicationTestingBuilder builder)
    {
        IResource[] containers =
        [
            builder.CreateResourceBuilder<PostgresServerResource>(Program.PostgresResource).Resource,
            builder.CreateResourceBuilder<KeycloakResource>(Program.KeycloakResource).Resource,
        ];
        foreach (var container in containers)
        {
            foreach (var volume in container.Annotations.OfType<ContainerMountAnnotation>().Where(mount => mount.Type == ContainerMountType.Volume).ToList())
            {
                container.Annotations.Remove(volume);
            }
        }
    }
}
