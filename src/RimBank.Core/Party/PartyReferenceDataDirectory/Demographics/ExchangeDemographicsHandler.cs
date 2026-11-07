using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Handles <see cref="ExchangeDemographics"/> on the write side (tech-stack §8.1, ADR 0027, ADR 0028).</summary>
public static class ExchangeDemographicsHandler
{
    /// <summary>
    /// Loads the entry; checks that it exists, that the Demographics id is its own and that the caller's expected
    /// version is current; decides the Exchange with the current time as the verification date and appends it with
    /// that expected version.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="directory">The context's write port.</param>
    /// <param name="time">The clock for the verification date.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The acknowledgement built from the decided state, <c>party.party-not-found</c>,
    /// <c>party.behavior-qualifier-not-found</c>, <c>party.version-conflict</c> or <c>party.demographics-not-recorded</c>.
    /// </returns>
    public static Task<Result<ExchangeDemographicsAcknowledged>> HandleAsync(
        ExchangeDemographics command,
        IPartyDirectory directory,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(time);

        return DemographicsCommandFlow.RunAsync(
            directory,
            command.EntryId,
            command.DemographicsId,
            command.ExpectedVersion,
            new ExchangeDemographicsDetails(command.DemographicsId, command.Action, time.GetUtcNow()),
            (entryId, demographicsId, version, demographics) =>
                new ExchangeDemographicsAcknowledged(entryId, demographicsId, version, demographics),
            cancellationToken);
    }
}
