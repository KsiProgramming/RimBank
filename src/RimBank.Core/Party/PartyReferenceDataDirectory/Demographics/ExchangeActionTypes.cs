using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Reads an <see cref="ExchangeActionType"/> from the name a caller sends, so every adapter (HTTP, MCP) accepts and
/// refuses exactly the same values (ADR 0028).
/// </summary>
public static class ExchangeActionTypes
{
    /// <summary>
    /// The action with this exact name: <c>Verify</c> or <c>Reject</c>. Names are case-sensitive and untrimmed, and an
    /// enum number is not a name.
    /// </summary>
    /// <param name="name">The name the caller sent, if any.</param>
    /// <returns>
    /// The action, <c>party.exchange-action-type-required</c> when the name is missing or blank, or
    /// <c>party.exchange-action-type-invalid</c> for any other value.
    /// </returns>
    public static Result<ExchangeActionType> FromName(string? name) =>
        name switch
        {
            null => DemographicsErrors.ExchangeActionTypeRequired,
            var blank when string.IsNullOrWhiteSpace(blank) => DemographicsErrors.ExchangeActionTypeRequired,
            nameof(ExchangeActionType.Verify) => ExchangeActionType.Verify,
            nameof(ExchangeActionType.Reject) => ExchangeActionType.Reject,
            _ => DemographicsErrors.ExchangeActionTypeInvalid,
        };
}
