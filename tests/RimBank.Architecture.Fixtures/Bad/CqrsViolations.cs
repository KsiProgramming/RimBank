using System.Diagnostics;
using RimBank.SharedKernel;

// Bad contexts for the CQRS, projection and aggregate rules (tech-stack §4, ADR 0010, ADR 0012).
// Each one breaks exactly one rule, named above it.

// command-handlers-stay-on-write-side: a command handler never reads through a read port.
namespace RimBank.SampleBanking.CommandHandlerReadsView
{
    public sealed record CommandHandlerReadsViewSummary(Guid Id, int Postings);

    public interface ICommandHandlerReadsViewEnquiry : IReadPort
    {
        Task<CommandHandlerReadsViewSummary?> Find(Guid id, CancellationToken cancellationToken);
    }

    public sealed record CloseCommandHandlerReadsView(Guid Id, IdempotencyKey IdempotencyKey) : ICommand;

    public sealed class CloseCommandHandlerReadsViewHandler(ICommandHandlerReadsViewEnquiry enquiry)
    {
        public async Task<Result<Guid>> HandleAsync(CloseCommandHandlerReadsView command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var summary = await enquiry.Find(command.Id, cancellationToken).ConfigureAwait(false);
            return summary is null ? Error.NotFound("sample.not-found", "Not found.") : summary.Id;
        }
    }
}

// query-handlers-stay-on-read-side: a query handler never loads through a write port.
namespace RimBank.SampleBanking.QueryHandlerUsesWritePort
{
    public sealed record QueryHandlerUsesWritePortView(Guid Id);

    public interface IQueryHandlerUsesWritePortDirectory : IWritePort
    {
        Task<Guid?> Load(Guid id, CancellationToken cancellationToken);
    }

    public sealed record RetrieveQueryHandlerUsesWritePort(Guid Id) : IQuery<QueryHandlerUsesWritePortView>;

    public sealed class RetrieveQueryHandlerUsesWritePortHandler(IQueryHandlerUsesWritePortDirectory directory)
    {
        public async Task<Result<QueryHandlerUsesWritePortView>> HandleAsync(RetrieveQueryHandlerUsesWritePort query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            var id = await directory.Load(query.Id, cancellationToken).ConfigureAwait(false);
            return id is null ? Error.NotFound("sample.not-found", "Not found.") : new QueryHandlerUsesWritePortView(id.Value);
        }
    }
}

// projections-implement-contract: a …Projection implements IProjection.
namespace RimBank.SampleBanking.ProjectionWithoutContract
{
    public sealed record ProjectionWithoutContractView(Guid Id);

    public static class ProjectionWithoutContractViewProjection
    {
        public static ProjectionWithoutContractView? Project(ProjectionWithoutContractView? view, Guid id) => view ?? new(id);
    }
}

// projections-are-pure: a projection never reads the clock; time comes from event data.
namespace RimBank.SampleBanking.ProjectionUsesTimeProvider
{
    public sealed record ProjectionUsesTimeProviderView(Guid Id, DateTimeOffset LastSeen);

    public sealed record ProjectionUsesTimeProviderSeen(Guid Id);

    public sealed class ProjectionUsesTimeProviderViewProjection
        : IProjection<ProjectionUsesTimeProviderView, ProjectionUsesTimeProviderSeen>
    {
        private ProjectionUsesTimeProviderViewProjection()
        {
        }

        public static ProjectionUsesTimeProviderView? Project(ProjectionUsesTimeProviderView? view, ProjectionUsesTimeProviderSeen domainEvent) =>
            domainEvent is null ? view : new(domainEvent.Id, TimeProvider.System.GetUtcNow());
    }
}

// aggregates-implement-decider: a state with Evolve implements IDecider.
namespace RimBank.SampleBanking.AggregateWithoutDecider
{
    public sealed record AggregateWithoutDeciderOpened(Guid Id);

    public sealed record AggregateWithoutDeciderEntry(Guid? Id)
    {
        public static AggregateWithoutDeciderEntry Evolve(AggregateWithoutDeciderEntry state, AggregateWithoutDeciderOpened domainEvent) =>
            domainEvent is null ? state : new(domainEvent.Id);
    }
}

// aggregates-implement-decider: an IDecider is a sealed record.
namespace RimBank.SampleBanking.DeciderAsClass
{
    public sealed record OpenDeciderAsClass(Guid Id);

    public sealed record DeciderAsClassOpened(Guid Id);

    public sealed class DeciderAsClassEntry : IDecider<DeciderAsClassEntry, OpenDeciderAsClass, DeciderAsClassOpened>
    {
        public static DeciderAsClassEntry Initial { get; } = new();

        public static Result<IReadOnlyList<DeciderAsClassOpened>> Decide(OpenDeciderAsClass command, DeciderAsClassEntry state) => command switch
        {
            not null => Decision.Accept(new DeciderAsClassOpened(command.Id)),
            _ => throw new UnreachableException(),
        };

        public static DeciderAsClassEntry Evolve(DeciderAsClassEntry state, DeciderAsClassOpened domainEvent) => state;
    }
}
