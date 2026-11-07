using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RimBank.Postgresql.Database.Tests;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;
using RimBank.Postgresql.FakeContexts.Tests;
using RimBank.SharedKernel;
using Wolverine.Runtime;

namespace RimBank.Postgresql.Registration.Tests;

// Roadmap 1.7: a read port is registered by convention, reads through query sessions of its context's own store, and
// its query handlers run without a transaction (ADR 0016, specs/tech-stack.md §8.2).
public sealed class ReadPortRegistrationTests(PostgresqlServer server)
{
    private const string NoServer = "Host=localhost;Port=1;Database=none;Username=rimbank_fake_ledger;Password=unused";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void The_read_port_resolves_to_its_implementation_once_for_the_whole_host()
    {
        // No server: resolving the port and its store opens no connection.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration["ConnectionStrings:FakeLedger"] = NoServer;
        builder.Configuration["ConnectionStrings:FakeRegistry"] = NoServer.Replace("fake_ledger", "fake_registry", StringComparison.Ordinal);
        builder.Configuration["ConnectionStrings:Wolverine"] = NoServer.Replace("fake_ledger", "wolverine", StringComparison.Ordinal);
        builder.UseTestBus();
        builder.AddFakeLedger();
        builder.AddFakeRegistry();
        using var host = builder.Build();

        var first = host.Services.GetRequiredService<IFakeLedgerEnquiry>();
        using var scope = host.Services.CreateScope();

        first.ShouldBeOfType<FakeLedgerEnquiry>();
        scope.ServiceProvider.GetRequiredService<IFakeLedgerEnquiry>().ShouldBeSameAs(first);
    }

    [Fact]
    public void A_read_port_that_takes_a_session_is_refused_because_it_could_write()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Should.Throw<ArgumentException>(() => builder.AddReadPort<IFakeLedgerEnquiry, EnquiryOverASession>())
            .Message.ShouldContain("store");
    }

    [Fact]
    public void A_read_port_that_takes_any_store_is_refused_because_it_must_read_its_own_context()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Should.Throw<ArgumentException>(() => builder.AddReadPort<IFakeLedgerEnquiry, EnquiryOverAnyStore>())
            .Message.ShouldContain("store");
    }

    [Fact]
    public async Task A_query_handler_that_takes_a_read_port_runs_without_a_transaction()
    {
        await using var host = await RunningHost.StartAsync(server);
        var ledger = Guid.CreateVersion7();
        await host.Bus.InvokeAsync<Result<FakeLedgerAcknowledged>>(new OpenFakeLedger(ledger, "Cash", Keys.New()), Cancellation);

        var view = await host.Bus.InvokeAsync<Result<FakeLedgerView>>(new RetrieveFakeLedger(ledger), Cancellation);

        view.Value.ShouldBe(new FakeLedgerView(ledger, "Cash", 0m));
        var handlers = ((WolverineRuntime)host.Services.GetRequiredService<IWolverineRuntime>()).Handlers;
        handlers.ChainFor<RetrieveFakeLedger>()!.IsTransactional.ShouldBeFalse();
        handlers.ChainFor<OpenFakeLedger>()!.IsTransactional.ShouldBeTrue();
    }

    private sealed class EnquiryOverASession(IDocumentSession session) : IFakeLedgerEnquiry
    {
        public Task<FakeLedgerView?> FindAsync(Guid ledgerId, CancellationToken cancellationToken) =>
            session.LoadAsync<FakeLedgerView>(ledgerId, cancellationToken);
    }

    private sealed class EnquiryOverAnyStore(IDocumentStore store) : IFakeLedgerEnquiry
    {
        public async Task<FakeLedgerView?> FindAsync(Guid ledgerId, CancellationToken cancellationToken)
        {
            await using var query = store.QuerySession();
            return await query.LoadAsync<FakeLedgerView>(ledgerId, cancellationToken);
        }
    }
}
