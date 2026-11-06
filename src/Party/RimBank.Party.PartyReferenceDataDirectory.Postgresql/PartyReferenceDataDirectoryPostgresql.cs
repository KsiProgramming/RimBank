using Marten;
using Microsoft.Extensions.Hosting;
using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Postgresql;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql;

/// <summary>Registers the Party Reference Data Directory's store and write port in the host (ADR 0014, ADR 0016, ADR 0017).</summary>
public static class PartyReferenceDataDirectoryPostgresql
{
    /// <summary>The BIAN service domain name: it derives the schema, role, connection string and queue (ADR 0016).</summary>
    public const string ServiceDomain = "PartyReferenceDataDirectory";

    /// <summary>
    /// The context's own store options. Every event has a stable stored name, so renaming a CLR type never breaks a
    /// stored stream.
    /// </summary>
    /// <param name="options">The store's options.</param>
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.MapEventType<PartyRegistered>("party_registered");
    }

    /// <summary>
    /// Adds the context's store, bound to its core's namespace for Wolverine handlers and idempotency, and
    /// <see cref="MartenPartyDirectory"/> as its <see cref="IPartyDirectory"/>.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static IHostApplicationBuilder AddPartyReferenceDataDirectoryPostgresql(this IHostApplicationBuilder builder)
    {
        builder.AddServiceDomainStore<IPartyReferenceDataDirectoryStore, IPartyDirectory>(ServiceDomain, Configure);
        builder.AddWritePort<IPartyDirectory, MartenPartyDirectory>();
        return builder;
    }
}
