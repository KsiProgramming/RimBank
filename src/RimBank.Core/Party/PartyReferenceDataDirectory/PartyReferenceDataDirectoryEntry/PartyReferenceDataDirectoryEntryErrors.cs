using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>The entry's business errors; their codes are documented in ADR 0024.</summary>
public static class PartyReferenceDataDirectoryEntryErrors
{
    /// <summary>Register on an entry that is already registered (409).</summary>
    public static Error PartyAlreadyRegistered { get; } = Error.Conflict(
        "party.party-already-registered",
        "This party is already registered in the directory.");

    /// <summary>The BIAN parameter that names an entry in every operation on it.</summary>
    public const string EntryIdField = "partyreferencedatadirectoryid";

    /// <summary>
    /// The entry id a caller sent is not an id the directory issues: not a GUID, empty, or not a version 7 GUID (400).
    /// It keeps the shared <c>shared.invalid-identifier</c> code of <see cref="TypedId"/> and names
    /// <see cref="EntryIdField"/>, so HTTP and MCP answer it the same way.
    /// </summary>
    public static Error EntryIdInvalid { get; } = Error.Validation(
        TypedId.InvalidIdentifierCode,
        "The value is not a valid identifier.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [EntryIdField] = ["The party reference data directory id must be the id returned by Register."],
        });

    /// <summary>The entry a command or a query names does not exist (404).</summary>
    public static Error PartyNotFound { get; } = Error.NotFound(
        "party.party-not-found",
        "This party is not registered in the directory.");

    /// <summary>
    /// The Demographics, Reference or BankRelations id a command or a query names is not the one of the entry (404,
    /// ADR 0024).
    /// </summary>
    public static Error BehaviorQualifierNotFound { get; } = Error.NotFound(
        "party.behavior-qualifier-not-found",
        "This behavior qualifier does not belong to the party's directory entry.");

    /// <summary>
    /// The version a command expected (its <c>If-Match</c>) is not the entry's current stream version: someone else
    /// changed the entry since the caller read it (409, ADR 0027).
    /// </summary>
    public static Error VersionConflict { get; } = Error.Conflict(
        "party.version-conflict",
        "The entry has changed since it was read; retrieve it again and retry with its current version.");

    /// <summary>A Register with no party type, or an empty one (400).</summary>
    public static Error PartyTypeRequired { get; } = Error.Validation(
        "party.party-type-required",
        "The party type is required.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["PartyType"] = ["The party type is required: Person or Organisation."],
        });

    /// <summary>A party type other than Person or Organisation (400).</summary>
    public static Error PartyTypeInvalid { get; } = Error.Validation(
        "party.party-type-invalid",
        "The party type is not valid.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["PartyType"] = ["The party type must be Person or Organisation."],
        });
}
