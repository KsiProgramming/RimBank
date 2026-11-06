using JasperFx.CodeGeneration;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace RimBank.Postgresql.Database.Tests;

/// <summary>
/// The message bus of a test host, set up as <c>RimBank.Host</c> sets up its own (ADR 0017), except for the durability
/// mode: each test host runs alone on its database, so it uses <see cref="DurabilityMode.Solo"/>.
/// </summary>
public static class TestBus
{
    public static IHostApplicationBuilder UseTestBus(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseWolverine(options =>
        {
            // An assembly without handlers: a handler is found only because its context includes its own assembly.
            options.ApplicationAssembly = typeof(ServiceDomainName).Assembly;
            options.Durability.Mode = DurabilityMode.Solo;
            options.Policies.AutoApplyTransactions();
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
            options.UseRuntimeCompilation();
            options.PersistMessagingNode(builder.Environment);
        });
        return builder;
    }
}
