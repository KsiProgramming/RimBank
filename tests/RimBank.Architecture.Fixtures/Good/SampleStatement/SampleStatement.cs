using RimBank.SampleBanking.SampleLedger.PublishedLanguage;
using Wolverine;

// A second good context, so the cross-context rules have something to check: its .Subscriptions adapter
// reads SampleLedger's published language, the one allowed cross-context reference (tech-stack §8.4).

namespace RimBank.SampleBanking.SampleStatement
{
    public sealed record RecordKnownSampleLedger(Guid SampleLedgerEntryId) : RimBank.SharedKernel.ICommand;
}

namespace RimBank.SampleBanking.SampleStatement.Subscriptions
{
    public static class SampleLedgerSubscription
    {
        public static RecordKnownSampleLedger? Translate(SampleLedgerEntryOpenedV1 integrationEvent, IMessageBus bus) =>
            integrationEvent is null || bus is null ? null : new(integrationEvent.SampleLedgerEntryId);
    }
}
