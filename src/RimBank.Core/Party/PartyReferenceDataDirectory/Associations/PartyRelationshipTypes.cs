using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads and writes the BIAN names of <see cref="PartyRelationshipType"/> (roadmap 1.11, ADR 0033).</summary>
public static class PartyRelationshipTypes
{
    /// <summary>The BIAN spelling of a relationship type, for example <c>PartyIsParentOfParty</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its BIAN name.</returns>
    public static string ToBianName(this PartyRelationshipType type) =>
        Enum.IsDefined(type)
            ? type.ToString()
            : throw new ArgumentOutOfRangeException(nameof(type), type, "Not a party relationship type.");

    /// <summary>
    /// Reads a relationship type a caller sent: trimmed, BIAN names matched without regard to case. An enum number is
    /// not a name.
    /// </summary>
    /// <param name="text">The text sent, not blank.</param>
    /// <returns>The type, or <c>party.association-invalid</c> naming <see cref="PartyAssociationFields.AssociateType"/>.</returns>
    public static Result<PartyRelationshipType> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return Enum.GetValues<PartyRelationshipType>()
            .Where(type => string.Equals(type.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            .Select(type => (PartyRelationshipType?)type)
            .FirstOrDefault() switch
        {
            { } type => type,
            null => PartyAssociationErrors.AssociateTypeInvalid,
        };
    }
}
