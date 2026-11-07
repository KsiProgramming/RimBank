using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="UpdateDemographics"/> on the write side (tech-stack §8.1, ADR 0024, ADR 0027).</summary>
public static class UpdateDemographicsHandler
{
    /// <summary>
    /// Loads the entry; checks that it exists, that the Demographics id is its own and that the caller's expected
    /// version is current; decides the Update and appends it with that expected version.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.party-not-found</c>,
    /// <c>party.behavior-qualifier-not-found</c> or <c>party.version-conflict</c>.
    /// </returns>
    public static Task<Result<UpdateDemographicsAcknowledged>> HandleAsync(
        UpdateDemographics command,
        IPartyDirectory directory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);

        return DemographicsCommandFlow.RunAsync(
            directory,
            command.EntryId,
            command.DemographicsId,
            command.ExpectedVersion,
            new UpdateDemographicsDetails(command.DemographicsId, command.Changes),
            (entryId, demographicsId, version, demographics) =>
                new UpdateDemographicsAcknowledged(entryId, demographicsId, version, demographics),
            cancellationToken);
    }
}
