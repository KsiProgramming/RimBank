using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Retrieval;

/// <summary>
/// Projects an entry's events into its <see cref="PartyReferenceDataDirectoryEntryView"/> (tech-stack §8.2, ADR 0024).
/// Pure: run inline by Marten in the context's <c>.Postgresql</c> adapter, and by the core's tests without a database.
/// </summary>
/// <remarks>
/// Every event of the entry's stream bumps <see cref="PartyReferenceDataDirectoryEntryView.Version"/>, even one whose
/// data the view does not show, so the view's version is always the stream version. An event before
/// <see cref="PartyRegistered"/> leaves no view.
/// </remarks>
public sealed class PartyReferenceDataDirectoryEntryViewProjection
    : IProjection<PartyReferenceDataDirectoryEntryView, PartyReferenceDataDirectoryEntryEvent>
{
    /// <inheritdoc />
    public static PartyReferenceDataDirectoryEntryView? Project(
        PartyReferenceDataDirectoryEntryView? view,
        PartyReferenceDataDirectoryEntryEvent domainEvent) =>
        (view, domainEvent) switch
        {
            (_, PartyRegistered registered) => new PartyReferenceDataDirectoryEntryView(
                registered.EntryId.Value,
                Version: 1,
                registered.PartyType,
                registered.OpenDate,
                registered.DemographicsId,
                registered.PartyReferenceId,
                registered.BankRelationsId),
            (null, _) => null,
            (_, _) => view with { Version = view.Version + 1 },
        };
}
