using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RimBank.Host.Modules;
using RimBank.Problems;
using RimBank.SharedKernel;

namespace RimBank.Host.Errors.Tests;

/// <summary>
/// A test-only module whose endpoints fail on purpose: one throws, and two return an <see cref="Error"/> the way a
/// <c>.Api</c> endpoint will (ADR 0015).
/// </summary>
internal sealed class FaultyServiceDomainModule : IServiceDomainModule
{
    /// <summary>The text of the thrown exception, which must never reach a response.</summary>
    public const string Secret = "internal-secret-connection-string";

    public static Error Missing { get; } = Error.NotFound("sample.sample-not-found", "No sample with this identifier.");

    public static Error Invalid { get; } = Error.Validation(
        "sample.invalid-sample",
        "The sample is not valid.",
        new Dictionary<string, IReadOnlyList<string>> { ["name"] = ["The name is required."] });

    public string ServiceDomain => "Faulty";

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void AddMcpTools(IMcpServerBuilder tools)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/faulty/boom", IResult () => throw new InvalidOperationException(Secret));
        endpoints.MapGet("/faulty/missing", () => Missing.ToHttpResult());
        endpoints.MapGet("/faulty/invalid", () => Invalid.ToHttpResult());
    }
}
