using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime;

namespace RimBank.Host.Tests;

// The host runs one Wolverine bus for every context (specs/tech-stack.md §1, ADR 0017). With no context registered, it
// opens no database connection, finds no handler and keeps its messages in memory.
public sealed class MessageBusTests
{
    [Fact]
    public async Task The_host_runs_one_message_bus()
    {
        await using var app = await HostWithoutContexts.StartAsync();

        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetService<IMessageBus>().ShouldNotBeNull();
        app.Services.GetServices<IWolverineRuntime>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task No_handler_is_found_unless_a_context_includes_its_own_assembly()
    {
        await using var app = await HostWithoutContexts.StartAsync();

        var runtime = app.Services.GetRequiredService<IWolverineRuntime>();

        runtime.Options.ApplicationAssembly.ShouldBe(typeof(Program).Assembly);
        ((WolverineRuntime)runtime).Handlers.Chains.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_bus_is_safe_to_run_on_several_host_instances()
    {
        await using var app = await HostWithoutContexts.StartAsync();

        app.Services.GetRequiredService<IWolverineRuntime>().Options.Durability.Mode.ShouldBe(DurabilityMode.Balanced);
    }
}
