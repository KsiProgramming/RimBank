using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="RegisterParty"/> on the write side (tech-stack §8.1).</summary>
public static class RegisterPartyHandler
{
    /// <summary>
    /// Creates the entry id, the ids of its Demographics, Reference and BankRelations, and the open date; decides
    /// Register against the entry's stored history; and starts the entry's stream only when the decision accepts.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="time">The clock for the new ids and the open date.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The acknowledgement built from the decided state, or the decision's <see cref="Error"/>.</returns>
    public static async Task<Result<RegisterPartyAcknowledged>> HandleAsync(
        RegisterParty command,
        IPartyDirectory directory,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(time);

        var register = new RegisterPartyReferenceDataDirectoryEntry(
            PartyReferenceDataDirectoryEntryId.New(time),
            command.PartyType,
            time.GetUtcNow(),
            DemographicsId.New(time),
            PartyReferenceId.New(time),
            BankRelationsId.New(time));

        var history = await directory.LoadEntryAsync(register.EntryId, cancellationToken);
        var state = history.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve);

        return PartyReferenceDataDirectoryEntry.Decide(register, state).Map(events =>
        {
            directory.StartEntry(register.EntryId, events);
            return Acknowledge(events.Aggregate(state, PartyReferenceDataDirectoryEntry.Evolve), history.Count + events.Count);
        });
    }

    private static RegisterPartyAcknowledged Acknowledge(PartyReferenceDataDirectoryEntry registered, long version) =>
        registered switch
        {
            {
                Id: { } entryId,
                PartyType: { } partyType,
                OpenDate: { } openDate,
                DemographicsId: { } demographicsId,
                PartyReferenceId: { } partyReferenceId,
                BankRelationsId: { } bankRelationsId,
            } => new RegisterPartyAcknowledged(entryId, version, partyType, openDate, demographicsId, partyReferenceId, bankRelationsId),
            _ => throw new UnreachableException("An accepted Register leaves the entry registered."),
        };
}
