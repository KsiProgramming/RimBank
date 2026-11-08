using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// What a BIAN <c>Associations/Register</c> records (roadmap 1.11, ADR 0033): the associate, how the owner relates to it,
/// and when the association holds. Adapters build it with <see cref="Create"/>.
/// </summary>
/// <param name="Associate">The associate's directory entry. Whether it is registered is the handler's check.</param>
/// <param name="AssociateType">How the owner relates to the associate.</param>
/// <param name="ValidityPeriod">When the association holds, if known.</param>
public sealed record PartyAssociationTerms(
    PartyReferenceDataDirectoryEntryId Associate,
    PartyRelationshipType AssociateType,
    AssociationValidityPeriod? ValidityPeriod)
{
    /// <summary>
    /// Reads the terms a caller sent. Fields RimBank does not record are refused first, so nothing is recorded partly.
    /// The associate and the type are required; a blank validity period means none.
    /// </summary>
    /// <param name="input">The values sent.</param>
    /// <param name="unsupportedFields">The BIAN paths of the fields sent with a value that RimBank does not record.</param>
    /// <returns>
    /// The terms, <c>party.association-field-not-supported</c> naming every unsupported field, or
    /// <c>party.association-invalid</c> naming every missing or invalid value.
    /// </returns>
    public static Result<PartyAssociationTerms> Create(PartyAssociationInput input, IReadOnlyCollection<string> unsupportedFields)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(unsupportedFields);
        if (PartyAssociationInputReader.Unsupported(unsupportedFields) is { } unsupported)
        {
            return unsupported;
        }

        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var associate = PartyAssociationInputReader.ReadAssociate(input.AssociatePartyReference, required: true, fieldErrors);
        var type = PartyAssociationInputReader.ReadType(input.AssociateType, required: true, fieldErrors);
        var period = PartyAssociationInputReader.ReadPeriod(input.ValidFromToDate, fieldErrors);

        return (fieldErrors.Count, associate, type) switch
        {
            (0, { } associateId, { } associateType) => new PartyAssociationTerms(associateId, associateType, period?.Value),
            _ => PartyAssociationErrors.AssociationInvalid(fieldErrors),
        };
    }
}
