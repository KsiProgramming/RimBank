using Marten;
using Microsoft.Extensions.Hosting;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.FakeContexts.Registry.Tests;

// A second test-only context, so the tests can prove that two contexts never see each other's schema, views, daemon,
// outbox or handlers.

// Core: no Marten, no Wolverine.

public interface IFakeRegistryEvent;

public sealed record FakeEntryRegistered(Guid EntryId, string Name) : IFakeRegistryEvent;

public sealed record FakeRegistryEntryView(Guid Id, string Name);

public sealed class FakeRegistryEntryViewProjection : IProjection<FakeRegistryEntryView, IFakeRegistryEvent>
{
    public static FakeRegistryEntryView? Project(FakeRegistryEntryView? view, IFakeRegistryEvent domainEvent) => domainEvent switch
    {
        FakeEntryRegistered registered => new FakeRegistryEntryView(registered.EntryId, registered.Name),
        _ => view,
    };
}

public interface IFakeRegistryBook : IWritePort
{
    void Start(Guid entryId, IFakeRegistryEvent domainEvent);
}

public sealed record RegisterFakeEntry(Guid EntryId, string Name) : ICommand;

public sealed record FakeEntryAcknowledged(Guid EntryId);

public static class RegisterFakeEntryHandler
{
    public static Result<FakeEntryAcknowledged> Handle(RegisterFakeEntry command, IFakeRegistryBook book)
    {
        book.Start(command.EntryId, new FakeEntryRegistered(command.EntryId, command.Name));
        return new FakeEntryAcknowledged(command.EntryId);
    }
}

// Adapter (.Postgresql).

public interface IFakeRegistryStore : IDocumentStore;

public sealed class FakeRegistryBook(IDocumentSession session) : IFakeRegistryBook
{
    public void Start(Guid entryId, IFakeRegistryEvent domainEvent) => session.Events.StartStream(entryId, domainEvent);
}

public static class FakeRegistry
{
    public const string ServiceDomain = "FakeRegistry";

    public static void Configure(StoreOptions options)
    {
        options.Events.MapEventType<FakeEntryRegistered>("fake_entry_registered");
        options.AddInlineView<FakeRegistryEntryView, FakeRegistryEntryViewProjection, IFakeRegistryEvent>();
    }

    public static IHostApplicationBuilder AddFakeRegistry(this IHostApplicationBuilder builder)
    {
        builder.AddServiceDomainStore<IFakeRegistryStore, IFakeRegistryEvent>(ServiceDomain, Configure);
        builder.AddWritePort<IFakeRegistryBook, FakeRegistryBook>();
        return builder;
    }
}
