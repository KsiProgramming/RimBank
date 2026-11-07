using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Retrieval;

/// <summary>The validation errors of a Retrieve request; their codes are documented in ADR 0024.</summary>
public static class RetrievePartyReferenceDataDirectoryEntryRequestErrors
{
    /// <summary>The route value that names the entry.</summary>
    public const string EntryIdField = "partyreferencedatadirectoryid";

    /// <summary>
    /// The route's entry id is not an identifier RimBank issues: not a GUID, empty, or not a version 7 GUID (400). It
    /// reuses the shared <c>shared.invalid-identifier</c> code, with the route value named in <c>errors</c>.
    /// </summary>
    public static Error EntryIdInvalid { get; } = Error.Validation(
        TypedId.InvalidIdentifierCode,
        "The value is not a valid identifier.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            [EntryIdField] = ["The party reference data directory id must be the id returned by Register."],
        });
}
