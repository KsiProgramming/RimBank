using RimBank.Host.Errors;
using RimBank.Host.Messaging;
using RimBank.Host.Modules;
using RimBank.ServiceDefaults;

namespace RimBank.Host;

/// <summary>
/// The composition of <c>RimBank.Host</c>: service defaults, Problem Details, the message bus, then each context's
/// module (ADR 0014, ADR 0015, ADR 0017).
/// </summary>
public static class HostComposition
{
    /// <summary>
    /// Applies the service defaults and the Problem Details baseline, registers <paramref name="modules"/>, builds the
    /// application, adds the exception handler and status-code pages, then maps the health probes and each module's
    /// endpoints.
    /// </summary>
    /// <param name="builder">A fresh web application builder.</param>
    /// <param name="modules">The modules to compose, normally <see cref="ServiceDomainModules.All"/>.</param>
    /// <returns>The built application, ready to run.</returns>
    public static WebApplication Build(WebApplicationBuilder builder, IReadOnlyList<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceDefaults();
        builder.Services.AddRimBankProblemDetails();
        builder.AddRimBankMessaging();
        builder.AddServiceDomainModules(modules);

        var app = builder.Build();
        app.UseRimBankProblemDetails();
        app.MapDefaultEndpoints();
        app.MapServiceDomainModules(modules);

        return app;
    }
}
