using System.Diagnostics;
using RimBank.SampleBanking.SampleLedger;
using RimBank.SampleBanking.SampleLedger.PublishedLanguage;
using RimBank.SharedKernel;

// Bad contexts for cross-context isolation (tech-stack §8.4). Each one breaks exactly one rule, named above it.

// cross-context-isolation: only .Subscriptions may reference another context's .PublishedLanguage.
namespace RimBank.SampleBanking.CoreUsesOtherPublishedLanguage
{
    public sealed record CoreUsesOtherPublishedLanguageEntry(SampleLedgerEntryOpenedV1 Opened);
}

// cross-context-isolation: .Subscriptions never references another context's core.
namespace RimBank.SampleBanking.SubscriptionsUsesOtherCore.Subscriptions
{
    public static class SubscriptionsUsesOtherCoreSubscription
    {
        public static Guid Translate(SampleLedgerEntryId id) => id.Value;
    }
}

// published-language-has-no-views: a view is private to its context, never published.
namespace RimBank.SampleBanking.PublishedLanguageExposesView.PublishedLanguage
{
    public sealed record PublishedLanguageExposesViewSummaryV1(Guid Id, int Postings);
}

namespace RimBank.SampleBanking.PublishedLanguageExposesView
{
    public abstract record PublishedLanguageExposesViewEvent;

    public sealed record PublishedLanguageExposesViewOpened(Guid Id) : PublishedLanguageExposesViewEvent;

    public sealed class PublishedLanguageExposesViewSummaryProjection
        : IProjection<PublishedLanguage.PublishedLanguageExposesViewSummaryV1, PublishedLanguageExposesViewEvent>
    {
        private PublishedLanguageExposesViewSummaryProjection()
        {
        }

        public static PublishedLanguage.PublishedLanguageExposesViewSummaryV1? Project(
            PublishedLanguage.PublishedLanguageExposesViewSummaryV1? view,
            PublishedLanguageExposesViewEvent domainEvent) => domainEvent switch
            {
                PublishedLanguageExposesViewOpened opened => new(opened.Id, 0),
                _ => throw new UnreachableException(),
            };
    }
}
