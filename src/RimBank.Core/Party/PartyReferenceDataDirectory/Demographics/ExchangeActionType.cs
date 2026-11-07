namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// What a BIAN <c>Demographics/Exchange</c> does with the party's current details: RimBank's extension field
/// <c>ExchangeActionType</c> (ADR 0028).
/// </summary>
public enum ExchangeActionType
{
    /// <summary>The bank verified the details.</summary>
    Verify,

    /// <summary>The bank rejected the details.</summary>
    Reject,
}
