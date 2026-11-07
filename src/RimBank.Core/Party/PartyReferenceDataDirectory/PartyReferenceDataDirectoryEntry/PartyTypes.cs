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

    /// <summary>
    /// The party type an Update asks for. No name means "keep the current type", so it has no result; any name sent is
    /// read like <see cref="FromBianName"/>, except that a blank one is invalid rather than missing.
    /// </summary>
    /// <param name="name">The name the caller sent, if any.</param>
    /// <returns>
    /// <c>null</c> when no name was sent, otherwise the party type or <c>party.party-type-invalid</c>.
    /// </returns>
    public static Result<PartyType>? FromOptionalBianName(string? name) =>
        name switch
        {
            null => null,
            var blank when string.IsNullOrWhiteSpace(blank) => PartyReferenceDataDirectoryEntryErrors.PartyTypeInvalid,
            _ => FromBianName(name),
        };
}
