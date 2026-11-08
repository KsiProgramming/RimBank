using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads and writes the names of <see cref="BankRelationLifecycleStatus"/> (roadmap 1.12, ADR 0035).</summary>
public static class BankRelationLifecycleStatuses
{
    /// <summary>The spelling of a lifecycle status in requests and responses, for example <c>Active</c>.</summary>
    /// <param name="status">The status.</param>
    /// <returns>Its name.</returns>
    public static string ToBianName(this BankRelationLifecycleStatus status) =>
        Enum.IsDefined(status)
            ? status.ToString()
            : throw new ArgumentOutOfRangeException(nameof(status), status, "Not a bank relation lifecycle status.");

    /// <summary>Reads a lifecycle status a caller sent: trimmed, names matched without regard to case.</summary>
    /// <param name="text">The text sent, not blank.</param>
    /// <returns>
    /// The status, or <c>party.bank-relations-invalid</c> naming
    /// <see cref="BankRelationsFields.RelationshipLifecycleStatusDescription"/>.
    /// </returns>
    public static Result<BankRelationLifecycleStatus> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return Enum.GetValues<BankRelationLifecycleStatus>()
            .Where(status => string.Equals(status.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            .Select(status => (BankRelationLifecycleStatus?)status)
            .FirstOrDefault() switch
        {
            { } status => status,
            null => BankRelationsErrors.LifecycleStatusInvalid,
        };
    }
}
