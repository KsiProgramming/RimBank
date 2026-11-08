using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's relationship with the bank, held by the entry's BankRelations Behavior Qualifier (roadmap 1.12, ADR 0035):
/// what the bank is to the party, which employee or unit of the bank it is, and when and how the relationship stands.
/// BIAN gives one object, so there is one relationship per entry. Each part is optional: an unknown one is <c>null</c>.
/// </summary>
/// <param name="Type">BIAN <c>BankRelationType</c>.</param>
/// <param name="BusinessUnitEmployee">
/// BIAN <c>BusinessUnitEmployeeReference.PartyReference</c>: the directory entry of the employee (a person) or the unit (an
/// organisation), checked on the write side when it is recorded. Only with a <see cref="Type"/>.
/// </param>
/// <param name="StartDate">BIAN <c>BankRelationReference.RelationshipStartDate</c>, in UTC.</param>
/// <param name="EndDate">BIAN <c>BankRelationReference.RelationshipEndDate</c>, in UTC, not before <see cref="StartDate"/>.</param>
/// <param name="LifecycleStatus">BIAN <c>BankRelationReference.RelationshipLifecycleStatus.StatusDescription</c>.</param>
public sealed record BankRelationsDetails(
    BankRelationType? Type,
    PartyReferenceDataDirectoryEntryId? BusinessUnitEmployee,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    BankRelationLifecycleStatus? LifecycleStatus)
{
    /// <summary>Nothing recorded: the BankRelations of an entry before its first Update.</summary>
    public static BankRelationsDetails Empty { get; } = new(null, null, null, null, null);

    /// <summary>Whether any part is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The details after a partial update: a part the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting details.</returns>
    public BankRelationsDetails Apply(BankRelationsChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new BankRelationsDetails(
            changes.Type.ApplyTo(Type),
            changes.BusinessUnitEmployee.ApplyTo(BusinessUnitEmployee),
            changes.StartDate.ApplyTo(StartDate),
            changes.EndDate.ApplyTo(EndDate),
            changes.LifecycleStatus.ApplyTo(LifecycleStatus));
    }

    /// <summary>
    /// The employee or unit an Update must check against the write side: the resulting reference when the changes set it
    /// or change the type it must fit. A reference the Update keeps, with its type, was checked when it was recorded.
    /// </summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The entry to check, or <c>null</c>.</returns>
    public PartyReferenceDataDirectoryEntryId? ReferenceToCheck(BankRelationsChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return changes.BusinessUnitEmployee is not null || changes.Type is not null
            ? Apply(changes).BusinessUnitEmployee
            : null;
    }
}
