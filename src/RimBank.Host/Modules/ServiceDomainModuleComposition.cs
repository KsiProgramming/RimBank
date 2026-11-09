namespace RimBank.Host;

/// <summary>Registers and maps a list of <see cref="IServiceDomainModule"/>s, in list order (ADR 0014).</summary>
public static class ServiceDomainModuleComposition
{
    /// <summary>
    /// Calls each module's <see cref="IServiceDomainModule.AddServices"/>, then each module's
    /// <see cref="IServiceDomainModule.AddOperationPolicies"/>, in list order, and registers the resulting
    /// <see cref="OperationPolicyCatalog"/> (ADR 0038).
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="modules">The modules to register.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// Two modules have the same service domain name, or a BIAN operation is given more than one policy.
    /// </exception>
    public static TBuilder AddServiceDomainModules<TBuilder>(this TBuilder builder, IReadOnlyList<IServiceDomainModule> modules)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(modules);

        var duplicates = modules
            .GroupBy(module => module.ServiceDomain, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Each service domain registers exactly one module, but these are registered more than once: {string.Join(", ", duplicates)}.");
        }

        foreach (var module in modules)
        {
            module.AddServices(builder);
        }

        var catalog = new OperationPolicyCatalog();
        var policies = new OperationPolicies(builder.Services.AddAuthorizationBuilder(), catalog);
        foreach (var module in modules)
        {
            module.AddOperationPolicies(policies);
        }

        builder.Services.AddSingleton(catalog);
        return builder;
    }

    /// <summary>Calls each module's <see cref="IServiceDomainModule.AddMcpTools"/>, in list order (ADR 0021).</summary>
    /// <param name="tools">The host's MCP server builder.</param>
    /// <param name="modules">The modules to register, the same list given to <see cref="AddServiceDomainModules{TBuilder}"/>.</param>
    /// <returns>The same MCP server builder.</returns>
    public static IMcpServerBuilder AddServiceDomainMcpTools(this IMcpServerBuilder tools, IReadOnlyList<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.AddMcpTools(tools);
        }

        return tools;
    }

    /// <summary>Calls each module's <see cref="IServiceDomainModule.MapEndpoints"/>, in list order.</summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <param name="modules">The modules to map, the same list given to <see cref="AddServiceDomainModules{TBuilder}"/>.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapServiceDomainModules(this IEndpointRouteBuilder endpoints, IReadOnlyList<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
