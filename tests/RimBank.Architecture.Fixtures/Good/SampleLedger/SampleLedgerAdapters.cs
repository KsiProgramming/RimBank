using Marten;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Wolverine;
using Wolverine.Marten;

// The good context's published language and adapters. Each one uses only what tech-stack §4 allows it.

namespace RimBank.SampleBanking.SampleLedger.PublishedLanguage
{
    /// <summary>Integration event: BCL types only, versioned by name.</summary>
    public sealed record SampleLedgerEntryOpenedV1(Guid SampleLedgerEntryId, string CurrencyCode);
}

namespace RimBank.SampleBanking.SampleLedger.Api
{
    public sealed record PostSampleAmountRequest(decimal Amount, string CurrencyCode);

    public static class SampleLedgerEndpoints
    {
        public static IResult? Post(PostSampleAmountRequest request, IMessageBus bus) => request is null || bus is null ? null : default;

        // bus.InvokeAsync<T>(…) compiles to a call on ICommandBus, an interface IMessageBus inherits.
        public static bool Dispatches(IMessageBus bus) => bus is ICommandBus;

        // An endpoint may narrow who can call it (per-operation policies, roadmap 1.15), never open it to anonymous callers.
        public static string Map(string endpoint) =>
            Microsoft.AspNetCore.Builder.AuthorizationEndpointConventionBuilderExtensions.RequireAuthorization(endpoint);
    }
}

namespace RimBank.SampleBanking.SampleLedger.Mcp
{
    public static class SampleLedgerTools
    {
        [McpServerTool]
        public static RetrieveSampleLedgerEntry? Retrieve(SampleLedgerEntryId id, IMessageBus bus) => bus is null ? null : new(id);
    }
}

namespace RimBank.SampleBanking.SampleLedger.Postgresql
{
    public sealed class MartenSampleLedgerDirectory(IDocumentSession session, IMartenOutbox outbox) : ISampleLedgerDirectory
    {
        public IDocumentSession Session { get; } = session;

        public IMartenOutbox Outbox { get; } = outbox;

        public Task<SampleLedgerEntry> Load(SampleLedgerEntryId id, CancellationToken cancellationToken) =>
            Task.FromResult(SampleLedgerEntry.Initial);
    }
}
