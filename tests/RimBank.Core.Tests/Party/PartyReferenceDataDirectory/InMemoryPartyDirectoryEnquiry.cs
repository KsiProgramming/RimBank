
namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

/// <summary>
/// An in-memory <see cref="IPartyDirectoryEnquiry"/> for the core's query handler tests. It is seeded with events and
/// folds them through the real <see cref="PartyReferenceDataDirectoryEntryViewProjection"/>, so a test reads the view a
/// rebuild would produce.
/// </summary>
internal sealed class InMemoryPartyDirectoryEnquiry : IPartyDirectoryEnquiry
{
    private readonly Dictionary<PartyReferenceDataDirectoryEntryId, PartyReferenceDataDirectoryEntryView> _entries = [];

    public Task<PartyReferenceDataDirectoryEntryView?> FindEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_entries.GetValueOrDefault(entryId));

    /// <summary>Projects the history of <paramref name="entryId"/>, as if its events had been committed earlier.</summary>
    public void Seed(PartyReferenceDataDirectoryEntryId entryId, params PartyReferenceDataDirectoryEntryEvent[] events)
    {
        var view = events.Aggregate((PartyReferenceDataDirectoryEntryView?)null, PartyReferenceDataDirectoryEntryViewProjection.Project);
        if (view is not null)
        {
            _entries[entryId] = view;
        }
    }

    private readonly Dictionary<PartyAssociationId, PartyAssociationView> _associations = [];

    public Task<PartyAssociationView?> FindAssociationAsync(PartyAssociationId associationId, CancellationToken cancellationToken) =>
        Task.FromResult(_associations.GetValueOrDefault(associationId));

    /// <summary>Projects the history of <paramref name="associationId"/>, as if its events had been committed earlier.</summary>
    public void Seed(PartyAssociationId associationId, params PartyAssociationEvent[] events)
    {
        var view = events.Aggregate((PartyAssociationView?)null, PartyAssociationViewProjection.Project);
        if (view is not null)
        {
            _associations[associationId] = view;
        }
    }
}
