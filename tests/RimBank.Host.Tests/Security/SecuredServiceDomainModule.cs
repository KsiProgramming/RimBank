using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RimBank.Host.Modules;
using Wolverine;

namespace RimBank.Host.Security.Tests;

/// <summary>
/// A test-only module that maps endpoints the way a context's <c>.Api</c> will, without opting in to authentication:
/// deny by default must protect them anyway (ADR 0019).
/// </summary>
public sealed class SecuredServiceDomainModule : IServiceDomainModule
{
    public string ServiceDomain => "Secured";

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.ConfigureWolverine(options => options.Discovery.IncludeType(typeof(WhoSentThisHandler)));
    }

    public void AddMcpTools(IMcpServerBuilder tools)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/secured/ping", () => "pong");

        // An endpoint whose policy no token satisfies: an authenticated caller gets a 403.
        endpoints.MapGet("/secured/never", () => "never")
            .RequireAuthorization(policy => policy.RequireClaim("scope", "rimbank.never-granted"));

        // Dispatches through the bus as a .Api endpoint does, and returns the user name the handler saw.
        endpoints.MapGet("/secured/who", async (IMessageBus bus, CancellationToken cancellation) =>
            Results.Text(await bus.InvokeAsync<string>(new WhoSentThis(), cancellation)));
    }
}

/// <summary>A message that asks its handler who sent it.</summary>
public sealed record WhoSentThis;

/// <summary>Answers with the user name on the envelope it handles, or an empty string.</summary>
public static class WhoSentThisHandler
{
    public static string Handle(WhoSentThis message, Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.UserName ?? string.Empty;
    }
}
