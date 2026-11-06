using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Registration;

/// <summary>
/// Register a party in the directory: BIAN <c>PartyReferenceDataDirectoryEntry/Register</c> (ADR 0024). Handled by
/// <see cref="RegisterPartyHandler"/>, which creates the entry and its ids and acknowledges with
/// <see cref="RegisterPartyAcknowledged"/>.
/// </summary>
/// <param name="PartyType">Whether the party is a person or an organisation.</param>
/// <param name="IdempotencyKey">The caller's key for this request (ADR 0018). The handler never reads it.</param>
public sealed record RegisterParty(PartyType PartyType, IdempotencyKey IdempotencyKey) : ICommand;
