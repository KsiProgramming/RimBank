using Marten;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.FakeContexts.Tests;

// A second test-only context, so the tests can prove that two contexts never see each other's schema, views or daemon.

public interface IFakeRegistryStore : IDocumentStore;

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

public static class FakeRegistry
{
    public const string ServiceDomain = "FakeRegistry";

    public static void Configure(StoreOptions options)
    {
        options.Events.MapEventType<FakeEntryRegistered>("fake_entry_registered");
        options.AddInlineView<FakeRegistryEntryView, FakeRegistryEntryViewProjection, IFakeRegistryEvent>();
    }
}
