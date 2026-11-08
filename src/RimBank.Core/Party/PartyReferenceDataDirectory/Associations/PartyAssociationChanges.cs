using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The changes a BIAN <c>Associations/Update</c> asks for: a partial update (roadmap 1.11, ADR 0033). <c>null</c> keeps
/// the current value. Adapters build it with <see cref="Create"/>.
/// </summary>
/// <param name="Associate">
/// The associate the caller named, if any. The associate never changes, so the decision only accepts the current one.
/// </param>
/// <param name="AssociateType">The new relationship type, if any. It cannot be cleared.</param>
/// <param name="ValidityPeriod">The change of the validity period: a change with no value clears it.</param>
public sealed record PartyAssociationChanges(
    PartyReferenceDataDirectoryEntryId? Associate,
    PartyRelationshipType? AssociateType,
    AssociationValidityPeriodChange? ValidityPeriod)
{
    /// <summary>No change at all: an Update that only records that the association was reviewed.</summary>
    public static PartyAssociationChanges None { get; } = new(null, null, null);

    /// <summary>
    /// Reads the changes a caller sent. Fields RimBank does not record are refused first, so nothing is updated partly.
    /// A blank associate or type is invalid (neither can be cleared); a blank validity period clears it.
    /// </summary>
    /// <param name="input">The values sent: <c>null</c> keeps.</param>
    /// <param name="unsupportedFields">The BIAN paths of the fields sent with a value that RimBank does not record.</param>
    /// <returns>
    /// The changes, <c>party.association-field-not-supported</c> naming every unsupported field, or
    /// <c>party.association-invalid</c> naming every invalid value.
    /// </returns>
    public static Result<PartyAssociationChanges> Create(PartyAssociationInput input, IReadOnlyCollection<string> unsupportedFields)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(unsupportedFields);
        if (PartyAssociationInputReader.Unsupported(unsupportedFields) is { } unsupported)
        {
            return unsupported;
        }

        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var changes = new PartyAssociationChanges(
            PartyAssociationInputReader.ReadAssociate(input.AssociatePartyReference, required: false, fieldErrors),
            PartyAssociationInputReader.ReadType(input.AssociateType, required: false, fieldErrors),
            PartyAssociationInputReader.ReadPeriod(input.ValidFromToDate, fieldErrors));

        return fieldErrors.Count switch
        {
            0 => changes,
            _ => PartyAssociationErrors.AssociationInvalid(fieldErrors),
        };
    }
}

/// <summary>A change of an association's validity period: <c>null</c> <see cref="Value"/> clears it.</summary>
/// <param name="Value">The new period, or <c>null</c> to clear it.</param>
public sealed record AssociationValidityPeriodChange(AssociationValidityPeriod? Value);
