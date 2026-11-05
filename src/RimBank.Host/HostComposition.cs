using RimBank.Host.Modules;
using RimBank.ServiceDefaults;

namespace RimBank.Host;

/// <summary>The composition of <c>RimBank.Host</c>: service defaults, then each context's module (ADR 0014).</summary>
public static class HostComposition
{
    /// <summary>
    /// Applies the service defaults, registers <paramref name="modules"/>, builds the application, maps the health
    /// probes and then each module's endpoints.
    /// </summary>
    /// <param name="builder">A fresh web application builder.</param>
    /// <param name="modules">The modules to compose, normally <see cref="ServiceDomainModules.All"/>.</param>
    /// <returns>The built application, ready to run.</returns>
    public static WebApplication Build(WebApplicationBuilder builder, IReadOnlyList<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceDefaults();
        builder.AddServiceDomainModules(modules);

        var app = builder.Build();
        app.MapDefaultEndpoints();
        app.MapServiceDomainModules(modules);

        return app;
    }
}
