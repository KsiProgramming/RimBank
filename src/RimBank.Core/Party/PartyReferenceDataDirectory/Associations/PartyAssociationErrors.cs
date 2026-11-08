using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>The Associations Behavior Qualifier's business errors; their codes are documented in ADR 0024 and ADR 0033.</summary>
public static class PartyAssociationErrors
{
    /// <summary>The code of <see cref="AssociationInvalid"/>.</summary>
    public const string AssociationInvalidCode = "party.association-invalid";

    /// <summary>The code of <see cref="AssociationFieldsNotSupported"/>.</summary>
    public const string AssociationFieldNotSupportedCode = "party.association-field-not-supported";

    /// <summary>
    /// The association id a caller sent is not an id the directory issues (400). It keeps the shared
    /// <c>shared.invalid-identifier</c> code of <see cref="TypedId"/> and names <see cref="PartyAssociationFields.AssociationsId"/>.
    /// </summary>
    public static Error AssociationIdInvalid { get; } = Error.Validation(
        TypedId.InvalidIdentifierCode,
        "The value is not a valid identifier.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [PartyAssociationFields.AssociationsId] = ["The associations id must be the AssociationsId returned by Associations/Register."],
        });

    /// <summary>One or more association values are not valid (400), with a message per BIAN field.</summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.association-invalid</c> error.</returns>
    public static Error AssociationInvalid(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(AssociationInvalidCode, "The association values are not valid.", fieldErrors);

    /// <summary>An <c>AssociateType</c> that is not a BIAN relationship type (400).</summary>
    public static Error AssociateTypeInvalid { get; } = AssociationInvalid(new Dictionary<string, IReadOnlyList<string>>
    {
        [PartyAssociationFields.AssociateType] =
        [
            "The associate type must be one of " + string.Join(", ", Enum.GetNames<PartyRelationshipType>()) + ".",
        ],
    });

    /// <summary>
    /// The request sent BIAN Associations fields RimBank does not record (400, ADR 0033), with a message per BIAN field.
    /// </summary>
    /// <param name="fieldErrors">The BIAN field names and their messages.</param>
    /// <returns>The <c>party.association-field-not-supported</c> error.</returns>
    public static Error AssociationFieldsNotSupported(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        Error.Validation(
            AssociationFieldNotSupportedCode,
            "The request sends Associations fields that RimBank does not record; nothing was recorded.",
            fieldErrors);

    /// <summary>Register on an association whose stream already exists (409). Ids are new, so only a replay meets it.</summary>
    public static Error AssociationAlreadyRegistered { get; } = Error.Conflict(
        "party.association-already-registered",
        "This association is already registered.");

    /// <summary>The association does not exist, or the path's entry is not its owner (404, ADR 0024).</summary>
    public static Error AssociationNotFound { get; } = Error.NotFound(
        "party.association-not-found",
        "This association does not exist for the party's directory entry.");

    /// <summary>The associate is the owning entry itself (422, ADR 0024).</summary>
    public static Error SelfAssociation { get; } = Error.BusinessRule(
        "party.self-association",
        "A party cannot be associated with itself.");

    /// <summary>The associate is not an entry of this directory (422, ADR 0024).</summary>
    public static Error AssociateNotRegistered { get; } = Error.BusinessRule(
        "party.associate-not-registered",
        "The associate is not registered in the directory; register it first.");

    /// <summary>An Update names another associate than the one registered (422, ADR 0033).</summary>
    public static Error AssociateCannotChange { get; } = Error.BusinessRule(
        "party.associate-cannot-change",
        "The associate of an association never changes; register a new association instead.");

    /// <summary>
    /// The version an Update expected (its <c>If-Match</c>) is not the association's current stream version (409,
    /// ADR 0027). Same code as the entry's conflict.
    /// </summary>
    public static Error VersionConflict { get; } = Error.Conflict(
        PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code,
        "The association has changed since it was read; retrieve it again and retry with its current version.");
}
