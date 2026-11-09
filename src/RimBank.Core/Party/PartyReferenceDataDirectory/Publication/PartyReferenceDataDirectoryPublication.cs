using Published = RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Maps the entry's decided domain events to the context's published language (ADR 0024, ADR 0036). It reads only the
/// domain event, never a view (tech-stack §8.4), and holds no I/O: the command handler supplies the stream version
/// the event produced and a new event id, and cascades the result into the outbox in the same transaction.
/// Only the Control Record is published; the Behavior Qualifier events have no published channel yet (ADR 0024).
/// </summary>
public static class PartyReferenceDataDirectoryPublication
{
    /// <summary>Maps a registration to <c>PartyReferenceDataDirectoryEntry/Created</c>.</summary>
    /// <param name="registered">The decided event.</param>
    /// <param name="version">The entry's stream version after the event.</param>
    /// <param name="eventId">The integration event's own id.</param>
    /// <returns>The integration event.</returns>
    public static Published.PartyReferenceDataDirectoryEntryCreated Created(PartyRegistered registered, long version, Guid eventId)
    {
        ArgumentNullException.ThrowIfNull(registered);
        return new Published.PartyReferenceDataDirectoryEntryCreated(
            eventId,
            registered.OpenDate,
            registered.EntryId.Value,
            version,
            Publish(registered.PartyType),
            Published.DirectoryEntryDateTypes.OpenDate,
            registered.OpenDate);
    }

    /// <summary>Maps an update of the entry to <c>PartyReferenceDataDirectoryEntry/Updated</c>.</summary>
    /// <param name="updated">The decided event.</param>
    /// <param name="version">The entry's stream version after the event.</param>
    /// <param name="eventId">The integration event's own id.</param>
    /// <returns>The integration event.</returns>
    public static Published.PartyReferenceDataDirectoryEntryUpdated Updated(PartyReferenceDataDirectoryEntryUpdated updated, long version, Guid eventId)
    {
        ArgumentNullException.ThrowIfNull(updated);
        return new Published.PartyReferenceDataDirectoryEntryUpdated(
            eventId,
            updated.RefreshDate,
            updated.EntryId.Value,
            version,
            Publish(updated.PartyType),
            Published.DirectoryEntryDateTypes.RefreshDate,
            updated.RefreshDate);
    }

    private static string Publish(PartyType partyType) =>
        partyType switch
        {
            PartyType.Person => Published.PartyTypes.Person,
            PartyType.Organisation => Published.PartyTypes.Organisation,
            _ => throw new ArgumentOutOfRangeException(nameof(partyType), partyType, "Not a party type."),
        };
}
