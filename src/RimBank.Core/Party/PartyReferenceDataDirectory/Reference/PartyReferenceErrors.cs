using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>The Reference Behavior Qualifier's business errors; their codes are documented in ADR 0024 and ADR 0029.</summary>
public static class PartyReferenceErrors
{
    /// <summary>The code of <see cref="ReferenceInvalid"/> and <see cref="IdentifierInvalid"/>.</summary>
    public const string ReferenceInvalidCode = "party.reference-invalid";

    /// <summary>The code of <see cref="ReferenceFieldsNotSupported"/>.</summary>
    public const string ReferenceFieldNotSupportedCode = "party.reference-field-not-supported";

    /// <summary>
    /// The Reference id a caller sent is not an id the directory issues (400). It keeps the shared
    /// <c>shared.invalid-identifier</c> code of <see cref="TypedId"/> and names <see cref="PartyReferenceFields.ReferenceId"/>.
    /// </summary>
    public static Error ReferenceIdInvalid { get; } = Error.Validation(
        TypedId.InvalidIdentifierCode,
        "The value is not a valid identifier.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [PartyReferenceFields.ReferenceId] = ["The reference id must be the PartyReferenceId returned by Register."],
        });

    /// <summary>One party identifier is not valid (400); its message says why. <see cref="PartyReferenceChanges"/> names the field.</summary>
    /// <param name="message">What is wrong with the identifier.</param>
    /// <returns>The <c>party.reference-invalid</c> error.</returns>
    public static Error IdentifierInvalid(string message) => Error.Validation(ReferenceInvalidCode, message);

    /// <summary>One or more party identifiers are not valid (400), with a message per BIAN field.</summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.reference-invalid</c> error.</returns>
    public static Error ReferenceInvalid(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(ReferenceInvalidCode, "The party identifiers are not valid.", fieldErrors);

    /// <summary>
    /// The Update sent Reference fields RimBank does not record yet (400, ADR 0029), with a message per BIAN field that
    /// names the roadmap phase that will record it.
    /// </summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.reference-field-not-supported</c> error.</returns>
    public static Error ReferenceFieldsNotSupported(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(
            ReferenceFieldNotSupportedCode,
            "The request sends Reference fields that RimBank does not record yet; nothing was updated.",
            fieldErrors);
}
