using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;

/// <summary>The entry's business errors; their codes are documented in ADR 0024.</summary>
public static class PartyReferenceDataDirectoryEntryErrors
{
    /// <summary>Register on an entry that is already registered (409).</summary>
    public static Error PartyAlreadyRegistered { get; } = Error.Conflict(
        "party.party-already-registered",
        "This party is already registered in the directory.");

    /// <summary>A party type other than Person or Organisation (400).</summary>
    public static Error PartyTypeInvalid { get; } = Error.Validation(
        "party.party-type-invalid",
        "The party type is not valid.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["PartyType"] = ["The party type must be Person or Organisation."],
        });
}
