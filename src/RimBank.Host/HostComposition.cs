using RimBank.Host.Errors;
using RimBank.Host.Messaging;
using RimBank.Host.Modules;
using RimBank.Host.Security;
using RimBank.ServiceDefaults;

namespace RimBank.Host;

/// <summary>
/// The composition of <c>RimBank.Host</c>: service defaults, Problem Details, authentication, the message bus, then
/// each context's module (ADR 0014, ADR 0015, ADR 0017, ADR 0019).
/// </summary>
public static class HostComposition
{
    /// <summary>
    /// Applies the service defaults, the Problem Details baseline and authentication, registers
    /// <paramref name="modules"/>, builds the application, adds the exception handler, status-code pages,
    /// authentication and authorization, then maps the health probes and each module's endpoints. Every endpoint needs
    /// an authenticated caller except the health probes (deny by default).
    /// </summary>
    /// <param name="builder">A fresh web application builder.</param>
    /// <param name="modules">The modules to compose, normally <see cref="ServiceDomainModules.All"/>.</param>
    /// <returns>The built application, ready to run.</returns>
    public static WebApplication Build(WebApplicationBuilder builder, IReadOnlyList<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceDefaults();
        builder.Services.AddRimBankProblemDetails();
        builder.AddRimBankAuthentication();
        builder.AddRimBankMessaging();
        builder.AddServiceDomainModules(modules);

        var app = builder.Build();
        app.UseRimBankProblemDetails();
        app.UseRimBankAuthentication();
        app.MapDefaultEndpoints();
        app.MapServiceDomainModules(modules);

        return app;
    }
}
