namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's residency status: BIAN Reference <c>ResidencyStatus</c> (ISO 20022 <c>Status</c>), of which RimBank records
/// the code in <c>StatusDescription</c> and the time it was assigned in <c>StatusDateTime</c> (roadmap 1.18, ADR 0029).
/// Only the current status is recorded. Personal data (7.2).
/// </summary>
/// <param name="Code">Resident or not resident.</param>
/// <param name="StatusDateTime">When the status was assigned, if known.</param>
public sealed record ResidencyStatus(ResidencyStatusCode Code, DateTimeOffset? StatusDateTime);
