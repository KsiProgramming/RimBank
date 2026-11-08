
namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql;

/// <summary>
/// The Marten implementation of <see cref="IPartyDirectoryEnquiry"/>: it reads the entry views that
/// <see cref="PartyReferenceDataDirectoryEntryViewProjection"/> projects inline in the context's own store (ADR 0016,
/// ADR 0024). Each read opens a short query session, so it never writes and never joins a transaction. It only maps
/// typed ids to document ids and holds no logic.
/// </summary>
/// <param name="store">The context's own store.</param>
public sealed class MartenPartyDirectoryEnquiry(IPartyReferenceDataDirectoryStore store) : IPartyDirectoryEnquiry
{
    /// <inheritdoc />
    public async Task<PartyReferenceDataDirectoryEntryView?> FindEntryAsync(
        PartyReferenceDataDirectoryEntryId entryId,
        CancellationToken cancellationToken)
    {
        await using var query = store.QuerySession();
        return await query.LoadAsync<PartyReferenceDataDirectoryEntryView>(entryId.Value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PartyAssociationView?> FindAssociationAsync(PartyAssociationId associationId, CancellationToken cancellationToken)
    {
        await using var query = store.QuerySession();
        return await query.LoadAsync<PartyAssociationView>(associationId.Value, cancellationToken);
    }
}
