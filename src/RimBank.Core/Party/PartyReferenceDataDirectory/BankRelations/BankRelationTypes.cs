using System.Diagnostics;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads and writes the names of <see cref="BankRelationType"/>, and the party each one references (roadmap 1.12, ADR 0035).</summary>
public static class BankRelationTypes
{
    /// <summary>The spelling of a bank relation type in requests and responses, for example <c>RelationshipManager</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its name.</returns>
    public static string ToBianName(this BankRelationType type) =>
        Enum.IsDefined(type)
            ? type.ToString()
            : throw new ArgumentOutOfRangeException(nameof(type), type, "Not a bank relation type.");

    /// <summary>
    /// The party type <c>BusinessUnitEmployeeReference</c> must name for this relation: a person for an employee's role, an
    /// organisation for a unit's.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><see cref="PartyType.Person"/> or <see cref="PartyType.Organisation"/>.</returns>
    public static PartyType ReferencedPartyType(this BankRelationType type) =>
        type switch
        {
            BankRelationType.RelationshipManager or BankRelationType.AccountOfficer => PartyType.Person,
            BankRelationType.ServicingBranch or BankRelationType.ServicingBusinessUnit => PartyType.Organisation,
            _ => throw new UnreachableException($"Not a bank relation type: {type}."),
        };

    /// <summary>
    /// Reads a bank relation type a caller sent: trimmed, names matched without regard to case. An enum number is not a
    /// name.
    /// </summary>
    /// <param name="text">The text sent, not blank.</param>
    /// <returns>The type, or <c>party.bank-relations-invalid</c> naming <see cref="BankRelationsFields.BankRelationType"/>.</returns>
    public static Result<BankRelationType> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return Enum.GetValues<BankRelationType>()
            .Where(type => string.Equals(type.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            .Select(type => (BankRelationType?)type)
            .FirstOrDefault() switch
        {
            { } type => type,
            null => BankRelationsErrors.BankRelationTypeInvalid,
        };
    }
}
