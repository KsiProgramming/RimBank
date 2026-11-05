using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RimBank.Host.Health.Tests;

// The probes are mapped in every environment, open to anonymous callers, and return the status only (ADR 0014).
public sealed class HealthEndpointTests
{
    public static TheoryData<string, string> ProbesByEnvironment => new()
    {
        { "/health", "Development" },
        { "/alive", "Development" },
        { "/health", "Production" },
        { "/alive", "Production" },
    };

    [Theory]
    [MemberData(nameof(ProbesByEnvironment))]
    public async Task Probe_returns_healthy_without_credentials(string path, string environment)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host => host.UseEnvironment(environment));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Health_body_exposes_no_check_names_or_details()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host => host.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldNotContain("self");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
    }
}
