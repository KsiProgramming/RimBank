using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>The Reference Behavior Qualifier's business errors; their codes are documented in ADR 0024, ADR 0029 and ADR 0034.</summary>
public static class PartyReferenceErrors
{
    /// <summary>The code of <see cref="ReferenceInvalid"/> and <see cref="ValueInvalid"/>.</summary>
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

    /// <summary>One Reference value is not valid (400); its message says why. <see cref="PartyReferenceChanges"/> names the field.</summary>
    /// <param name="message">What is wrong with the value.</param>
    /// <returns>The <c>party.reference-invalid</c> error.</returns>
    public static Error ValueInvalid(string message) => Error.Validation(ReferenceInvalidCode, message);

    /// <summary>One or more Reference values are not valid (400), with a message per BIAN field.</summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.reference-invalid</c> error.</returns>
    public static Error ReferenceInvalid(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(ReferenceInvalidCode, "The Reference values are not valid.", fieldErrors);

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

    /// <summary>
    /// The Update sets a date of birth after today (400, roadmap 1.18): <c>party.reference-invalid</c> naming
    /// <see cref="PartyReferenceFields.DateofBirth"/>. The decision checks it against the handler's clock.
    /// </summary>
    public static Error DateOfBirthAfterToday { get; } = ReferenceInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [PartyReferenceFields.DateofBirth] = ["The date of birth must not be after today."],
    });

    /// <summary>
    /// The Update sets a customer-since date after today (400, roadmap 1.20): <c>party.reference-invalid</c> naming
    /// <see cref="PartyReferenceFields.CustomerSinceDate"/>. The decision checks it against the handler's clock.
    /// </summary>
    public static Error CustomerSinceDateAfterToday { get; } = ReferenceInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [PartyReferenceFields.CustomerSinceDate] = ["The customer-since date must not be after today."],
    });

    /// <summary>
    /// The resulting Reference has a customer-since date before the date of birth (400, roadmap 1.20, ADR 0032):
    /// <c>party.reference-invalid</c> naming <see cref="PartyReferenceFields.CustomerSinceDate"/>.
    /// </summary>
    public static Error CustomerSinceDateBeforeDateOfBirth { get; } = ReferenceInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [PartyReferenceFields.CustomerSinceDate] = ["The customer-since date must not be before the date of birth."],
    });

    /// <summary>
    /// The resulting Reference has a political exposure description without a foreign or domestic exposure (400, roadmap
    /// 1.20, ADR 0032): <c>party.reference-invalid</c> naming <see cref="PartyReferenceFields.PoliticalExposureDescriptionRecord"/>.
    /// </summary>
    public static Error PoliticalExposureDescriptionWithoutExposure { get; } = ReferenceInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [PartyReferenceFields.PoliticalExposureDescriptionRecord] =
        [
            "A political exposure description needs a PoliticalExposureType of Politicalexposureforeign or "
            + "Politicalexposuredomestic; clear the description in the same request when there is no exposure.",
        ],
    });

    /// <summary>
    /// The resulting Reference has a company officer role but no company officer (400, roadmap 1.21, ADR 0034):
    /// <c>party.reference-invalid</c> naming <see cref="PartyReferenceFields.CompanyOfficerRole"/>.
    /// </summary>
    public static Error CompanyOfficerRoleWithoutOfficer { get; } = ReferenceInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [PartyReferenceFields.CompanyOfficerRole] =
        [
            "A company officer role needs a CompanyOfficerReference; clear the role in the same request when the officer is cleared.",
        ],
    });

    /// <summary>
    /// The resulting Reference has corporate or officer references on an entry that is not an organisation, or an entry
    /// Update makes an organisation with such references a person (422, roadmap 1.21, ADR 0034).
    /// </summary>
    public static Error CorporateReferencesNeedOrganisation { get; } = Error.BusinessRule(
        "party.corporate-references-need-organisation",
        "Corporate customer, legal entity and company officer references are recorded only on an organisation's entry; "
        + "clear them before the party type becomes Person.");

    /// <summary>A reference names the entry it is recorded on (422, roadmap 1.21, ADR 0034).</summary>
    /// <param name="field">The BIAN path of the reference, for example <c>CompanyOfficerReference.PartyReference</c>.</param>
    /// <returns>The <c>party.self-reference</c> error, whose message names the path.</returns>
    public static Error SelfReference(string field) => Error.BusinessRule(
        "party.self-reference",
        $"{field} names the party's own entry; a party cannot reference itself.");

    /// <summary>A reference names no registered entry of this directory (422, roadmap 1.21, ADR 0034).</summary>
    /// <param name="field">The BIAN path of the reference.</param>
    /// <returns>The <c>party.referenced-party-not-registered</c> error, whose message names the path.</returns>
    public static Error ReferencedPartyNotRegistered(string field) => Error.BusinessRule(
        "party.referenced-party-not-registered",
        $"{field} is not registered in the directory; register it first.");

    /// <summary>
    /// A reference names an entry of the wrong party type: the corporate customer and the legal entity are organisations,
    /// and the company officer is a person (422, roadmap 1.21, ADR 0034).
    /// </summary>
    /// <param name="field">The BIAN path of the reference.</param>
    /// <param name="expected">The party type the reference needs.</param>
    /// <returns>The <c>party.referenced-party-type-mismatch</c> error, whose message names the path and the type.</returns>
    public static Error ReferencedPartyTypeMismatch(string field, PartyType expected) => Error.BusinessRule(
        "party.referenced-party-type-mismatch",
        $"{field} must name a party of type {expected}.");
}
