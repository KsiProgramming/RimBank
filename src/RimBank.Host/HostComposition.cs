using Microsoft.Extensions.DependencyInjection.Extensions;
using RimBank.ServiceDefaults;

namespace RimBank.Host;

/// <summary>
/// The composition of <c>RimBank.Host</c>: service defaults, Problem Details, authentication, the message bus, each
/// context's module, and the MCP endpoint with the modules' tools (ADR 0014, ADR 0015, ADR 0017, ADR 0019, ADR 0021).
/// </summary>
public static class HostComposition
{
    /// <summary>
    /// Applies the service defaults, the Problem Details baseline and authentication, registers
    /// <paramref name="modules"/>, builds the application, adds the exception handler, status-code pages,
    /// authentication and authorization, then maps the health probes, the MCP endpoint and each module's endpoints. Every endpoint needs
    /// an authenticated caller except the health probes (deny by default).
    /// </summary>
    /// <param name="builder">A fresh web application builder.</param>
    /// <param name="modules">The modules to compose, normally <see cref="ServiceDomainModules.All"/>.</param>
    /// <returns>The built application, ready to run.</returns>
    public static WebApplication Build(WebApplicationBuilder builder, IReadOnlyList<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceDefaults();

        // Command handlers take the time through TimeProvider (specs/tech-stack.md §7); a test host may supply its own.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddRimBankProblemDetails();
        builder.Services.AddRimBankOpenApi();
        builder.AddRimBankAuthentication();
        builder.AddRimBankMessaging();
        builder.AddServiceDomainModules(modules);
        builder.AddRimBankMcp(modules);

        var app = builder.Build();
        app.UseRimBankProblemDetails();
        app.UseRimBankAuthentication();
        app.MapDefaultEndpoints();
        app.MapRimBankMcp();
        app.MapServiceDomainModules(modules);

        return app;
    }
}
