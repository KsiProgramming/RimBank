using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Reads a <see cref="PartyType"/> from the BIAN name a caller sends (<c>Partytypevalues</c>), so every adapter (HTTP,
/// MCP) accepts and refuses exactly the same values (ADR 0024).
/// </summary>
public static class PartyTypes
{
    /// <summary>
    /// The party type with this exact BIAN name: <c>Person</c> or <c>Organisation</c>. Names are case-sensitive and
    /// untrimmed, and an enum number is not a name.
    /// </summary>
    /// <param name="name">The name the caller sent, if any.</param>
    /// <returns>
    /// The party type, <c>party.party-type-required</c> when the name is missing or blank, or
    /// <c>party.party-type-invalid</c> for any other value.
    /// </returns>
    public static Result<PartyType> FromBianName(string? name) =>
        name switch
        {
            null => PartyReferenceDataDirectoryEntryErrors.PartyTypeRequired,
            var blank when string.IsNullOrWhiteSpace(blank) => PartyReferenceDataDirectoryEntryErrors.PartyTypeRequired,
            nameof(PartyType.Person) => PartyType.Person,
            nameof(PartyType.Organisation) => PartyType.Organisation,
            _ => PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid,
        };
}
