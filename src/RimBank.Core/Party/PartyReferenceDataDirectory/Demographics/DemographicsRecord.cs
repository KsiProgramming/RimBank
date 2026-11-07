namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The record of the entry's Demographics Behavior Qualifier, an entity of the entry (ADR 0024): the party's details
/// and whether the bank has verified them (ADR 0028). Shared by the entry's state and its view.
/// </summary>
/// <param name="Details">The party's demographic details.</param>
/// <param name="VerificationStatus">Whether the bank has verified the current details.</param>
/// <param name="VerificationDate">When the bank last verified or rejected the current details; <c>null</c> while unverified.</param>
public sealed record DemographicsRecord(
    PartyDemographics Details,
    DemographicsVerificationStatus VerificationStatus,
    DateTimeOffset? VerificationDate)
{
    /// <summary>The Demographics of a newly registered entry: no details, unverified.</summary>
    public static DemographicsRecord Empty { get; } = new(PartyDemographics.Empty, DemographicsVerificationStatus.Unverified, null);

    /// <summary>
    /// The Demographics after an Update to <paramref name="details"/>: changed details are unverified again, and
    /// unchanged ones keep their verification.
    /// </summary>
    /// <param name="details">The details after the Update.</param>
    /// <returns>The resulting record.</returns>
    public DemographicsRecord UpdatedTo(PartyDemographics details) =>
        details == Details ? this : new DemographicsRecord(details, DemographicsVerificationStatus.Unverified, null);
}
