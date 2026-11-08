using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The BankRelations Behavior Qualifier's own errors; their codes are documented in ADR 0024 and ADR 0035. A reference to
/// another party that breaks a rule answers the shared reference errors of <see cref="PartyReferenceErrors"/>.
/// </summary>
public static class BankRelationsErrors
{
    /// <summary>The code of <see cref="BankRelationsInvalid"/>.</summary>
    public const string BankRelationsInvalidCode = "party.bank-relations-invalid";

    /// <summary>The code of <see cref="BankRelationsFieldsNotSupported"/>.</summary>
    public const string BankRelationsFieldNotSupportedCode = "party.bank-relations-field-not-supported";

    /// <summary>
    /// The BankRelations id a caller sent is not an id the directory issues (400). It keeps the shared
    /// <c>shared.invalid-identifier</c> code of <see cref="TypedId"/> and names <see cref="BankRelationsFields.BankRelationsId"/>.
    /// </summary>
    public static Error BankRelationsIdInvalid { get; } = Error.Validation(
        TypedId.InvalidIdentifierCode,
        "The value is not a valid identifier.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [BankRelationsFields.BankRelationsId] = ["The bank relations id must be the BankRelationsId returned by Register."],
        });

    /// <summary>One or more BankRelations values are not valid (400), with a message per BIAN field.</summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.bank-relations-invalid</c> error.</returns>
    public static Error BankRelationsInvalid(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(BankRelationsInvalidCode, "The BankRelations values are not valid.", fieldErrors);

    /// <summary>A <c>BankRelationType</c> that is not one RimBank records (400).</summary>
    public static Error BankRelationTypeInvalid { get; } = BankRelationsInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [BankRelationsFields.BankRelationType] =
        [
            "The bank relation type must be one of " + string.Join(", ", Enum.GetNames<BankRelationType>()) + ".",
        ],
    });

    /// <summary>A lifecycle status that is not one RimBank records (400).</summary>
    public static Error LifecycleStatusInvalid { get; } = BankRelationsInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [BankRelationsFields.RelationshipLifecycleStatusDescription] =
        [
            "The lifecycle status must be one of " + string.Join(", ", Enum.GetNames<BankRelationLifecycleStatus>()) + ".",
        ],
    });

    /// <summary>
    /// The resulting BankRelations has an employee or unit but no relation type, which says what kind of party it must be
    /// (400): <c>party.bank-relations-invalid</c> naming <see cref="BankRelationsFields.BankRelationType"/>.
    /// </summary>
    public static Error ReferenceWithoutType { get; } = BankRelationsInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [BankRelationsFields.BankRelationType] =
        [
            "A BusinessUnitEmployeeReference needs a BankRelationType; clear the reference in the same request when the type is cleared.",
        ],
    });

    /// <summary>
    /// The resulting BankRelations ends before it starts (400): <c>party.bank-relations-invalid</c> naming
    /// <see cref="BankRelationsFields.RelationshipEndDateContent"/>.
    /// </summary>
    public static Error EndDateBeforeStartDate { get; } = BankRelationsInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [BankRelationsFields.RelationshipEndDateContent] = ["The relationship must not end before it starts."],
    });

    /// <summary>
    /// The request sent BIAN BankRelations fields or parts RimBank does not record (400, ADR 0035), with a message per
    /// BIAN path.
    /// </summary>
    /// <param name="fieldErrors">The BIAN paths and their messages.</param>
    /// <returns>The <c>party.bank-relations-field-not-supported</c> error.</returns>
    public static Error BankRelationsFieldsNotSupported(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(
            BankRelationsFieldNotSupportedCode,
            "The request sends BankRelations fields that RimBank does not record; nothing was updated.",
            fieldErrors);
}
