using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>The Demographics Behavior Qualifier's business errors; their codes are documented in ADR 0024 and ADR 0028.</summary>
public static class DemographicsErrors
{
    /// <summary>
    /// The Demographics id a caller sent is not an id the directory issues (400). It keeps the shared
    /// <c>shared.invalid-identifier</c> code of <see cref="TypedId"/> and names <see cref="DemographicsFields.DemographicsId"/>.
    /// </summary>
    public static Error DemographicsIdInvalid { get; } = Error.Validation(
        TypedId.InvalidIdentifierCode,
        "The value is not a valid identifier.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [DemographicsFields.DemographicsId] = ["The demographics id must be the DemographicsId returned by Register."],
        });

    /// <summary>One or more demographic details are too long (400), with a message per BIAN field.</summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.demographics-invalid</c> error.</returns>
    public static Error DemographicsInvalid(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation("party.demographics-invalid", "The demographic details are not valid.", fieldErrors);

    /// <summary>An Exchange with no <c>ExchangeActionType</c>, or a blank one (400).</summary>
    public static Error ExchangeActionTypeRequired { get; } = Error.Validation(
        "party.exchange-action-type-required",
        "The exchange action type is required.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [DemographicsFields.ExchangeActionType] = ["The exchange action type is required: Verify or Reject."],
        });

    /// <summary>An <c>ExchangeActionType</c> other than Verify or Reject (400).</summary>
    public static Error ExchangeActionTypeInvalid { get; } = Error.Validation(
        "party.exchange-action-type-invalid",
        "The exchange action type is not valid.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [DemographicsFields.ExchangeActionType] = ["The exchange action type must be Verify or Reject."],
        });

    /// <summary>An Exchange on an entry whose Demographics has no details to verify or reject (422).</summary>
    public static Error DemographicsNotRecorded { get; } = Error.BusinessRule(
        "party.demographics-not-recorded",
        "The party has no demographic details to verify or reject; update them first.");
}
