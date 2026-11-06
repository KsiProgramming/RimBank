using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RimBank.Postgresql.Database.Tests;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;

namespace RimBank.Postgresql.Registration.Tests;

// The registration convention without a database: building the host and resolving a store opens no connection.
public sealed class ServiceDomainStoreRegistrationTests
{
    private const string NoServer = "Host=localhost;Port=1;Database=none;Username=rimbank_fake_ledger;Password=unused";

    [Fact]
    public void Each_context_gets_its_own_store_with_its_own_schema_for_events_and_documents()
    {
        using var host = Build("Development");

        var ledger = host.Services.GetRequiredService<IFakeLedgerStore>();
        var registry = host.Services.GetRequiredService<IFakeRegistryStore>();

        ledger.ShouldNotBeSameAs(registry);
        ledger.Options.DatabaseSchemaName.ShouldBe("fake_ledger");
        ledger.Options.Events.DatabaseSchemaName.ShouldBe("fake_ledger");
        registry.Options.DatabaseSchemaName.ShouldBe("fake_registry");
        registry.Options.Events.DatabaseSchemaName.ShouldBe("fake_registry");
    }

    [Fact]
    public void There_is_no_main_store_so_no_context_owns_a_default_one()
    {
        using var host = Build("Development");

        host.Services.GetService<IDocumentStore>().ShouldBeNull();
    }

    [Theory]
    [InlineData("Development", AutoCreate.CreateOrUpdate)]
    [InlineData("Staging", AutoCreate.CreateOrUpdate)]
    [InlineData("Production", AutoCreate.None)]
    public void Schema_objects_are_created_automatically_everywhere_but_in_production(string environment, AutoCreate expected)
    {
        using var host = Build(environment);

        ((StoreOptions)host.Services.GetRequiredService<IFakeLedgerStore>().Options).AutoCreateSchemaObjects.ShouldBe(expected);
    }

    [Fact]
    public void Each_store_registers_a_ready_health_check_named_after_its_context()
    {
        using var host = Build("Development");

        var registrations = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registrations.Select(registration => registration.Name).ShouldBe(["postgresql-fake-ledger", "postgresql-fake-registry", "postgresql-wolverine"], ignoreOrder: true);
        registrations.ShouldAllBe(registration => registration.Tags.Contains(ServiceDomainStoreRegistration.ReadyTag));
    }

    [Fact]
    public void A_context_registers_its_store_once()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddServiceDomainStore<IFakeLedgerStore, IFakeLedgerEvent>(FakeLedger.ServiceDomain);

        Should.Throw<InvalidOperationException>(() => builder.AddServiceDomainStore<IFakeLedgerStore, IFakeLedgerEvent>(FakeLedger.ServiceDomain))
            .Message.ShouldContain(FakeLedger.ServiceDomain);
    }

    [Fact]
    public void The_host_message_store_name_is_reserved()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Should.Throw<ArgumentException>(() => builder.AddServiceDomainStore<IFakeLedgerStore, IFakeLedgerEvent>("Wolverine"))
            .Message.ShouldContain("reserved");
    }

    [Fact]
    public void A_write_port_implementation_must_take_only_the_handlers_session()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Should.Throw<ArgumentException>(() => builder.AddWritePort<IFakeLedgerJournal, JournalWithoutSession>())
            .Message.ShouldContain("IDocumentSession");
    }

    [Fact]
    public void A_missing_connection_string_names_the_setting_and_the_role()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddServiceDomainStore<IFakeLedgerStore, IFakeLedgerEvent>(FakeLedger.ServiceDomain);
        using var host = builder.Build();

        var exception = Should.Throw<InvalidOperationException>(() => host.Services.GetRequiredService<IFakeLedgerStore>());

        exception.ToString().ShouldContain("ConnectionStrings:FakeLedger");
        exception.ToString().ShouldContain("rimbank_fake_ledger");
    }

    private static IHost Build(string environment)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        builder.Configuration["ConnectionStrings:FakeLedger"] = NoServer;
        builder.Configuration["ConnectionStrings:FakeRegistry"] = NoServer.Replace("fake_ledger", "fake_registry", StringComparison.Ordinal);
        builder.Configuration["ConnectionStrings:Wolverine"] = NoServer.Replace("fake_ledger", "wolverine", StringComparison.Ordinal);
        builder.UseTestBus();
        builder.AddFakeLedger();
        builder.AddFakeRegistry();
        return builder.Build();
    }
}

/// <summary>A write port implementation that does not take the handler's session, so it would write outside the transaction.</summary>
public sealed class JournalWithoutSession : IFakeLedgerJournal
{
    public Task<IReadOnlyList<IFakeLedgerEvent>> LoadAsync(Guid ledgerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IFakeLedgerEvent>>([]);

    public void Start(Guid ledgerId, IFakeLedgerEvent domainEvent)
    {
    }

    public void Append(Guid ledgerId, IFakeLedgerEvent domainEvent)
    {
    }
}
