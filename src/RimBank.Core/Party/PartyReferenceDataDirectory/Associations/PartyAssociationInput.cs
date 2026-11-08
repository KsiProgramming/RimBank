using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The raw values an adapter received for an association (BIAN <c>RegisterAssociations</c> or <c>UpdateAssociations</c>,
/// roadmap 1.11). <see cref="PartyAssociationTerms.Create"/> and <see cref="PartyAssociationChanges.Create"/> read them, so
/// HTTP and MCP accept and refuse exactly the same values.
/// </summary>
/// <param name="AssociatePartyReference">BIAN <c>AssociateReference.PartyReference</c>, if sent.</param>
/// <param name="AssociateType">BIAN <c>AssociateType</c>, if sent.</param>
/// <param name="ValidFromToDate">BIAN <c>AssociationValidFromToDate</c>, if sent.</param>
public sealed record PartyAssociationInput(
    string? AssociatePartyReference,
    string? AssociateType,
    AssociationValidityPeriodInput? ValidFromToDate);

/// <summary>Reads the values of a <see cref="PartyAssociationInput"/>, gathering every invalid one.</summary>
internal static class PartyAssociationInputReader
{
    /// <summary>
    /// <c>party.association-field-not-supported</c> naming every field in <paramref name="unsupportedFields"/>, if any.
    /// </summary>
    public static Error? Unsupported(IReadOnlyCollection<string> unsupportedFields) =>
        unsupportedFields.Count switch
        {
            0 => null,
            _ => PartyAssociationErrors.AssociationFieldsNotSupported(unsupportedFields
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    field => field,
                    IReadOnlyList<string> (_) => ["RimBank does not record this field."],
                    StringComparer.Ordinal)),
        };

    public static PartyReferenceDataDirectoryEntryId? ReadAssociate(
        string? text,
        bool required,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required || text is not null)
            {
                fieldErrors[PartyAssociationFields.AssociatePartyReference] =
                    ["The associate is required: the PartyReferenceDataDirectoryEntryId of a registered party."];
            }

            return null;
        }

        var associate = PartyReferenceDataDirectoryEntryId.TryCreate(text.Trim());
        if (associate.IsFailure)
        {
            fieldErrors[PartyAssociationFields.AssociatePartyReference] =
                ["The associate must be the PartyReferenceDataDirectoryEntryId returned by Register."];
            return null;
        }

        return associate.Value;
    }

    public static PartyRelationshipType? ReadType(
        string? text,
        bool required,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required || text is not null)
            {
                fieldErrors[PartyAssociationFields.AssociateType] = ["The associate type is required."];
            }

            return null;
        }

        var type = PartyRelationshipTypes.Read(text);
        if (type.IsFailure)
        {
            foreach (var (field, messages) in type.Error.FieldErrors)
            {
                fieldErrors[field] = messages;
            }

            return null;
        }

        return type.Value;
    }

    /// <summary>The period sent: <c>null</c> when none was sent, a change with no value when a blank one was sent.</summary>
    public static AssociationValidityPeriodChange? ReadPeriod(
        AssociationValidityPeriodInput? input,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (input is null)
        {
            return null;
        }

        if (input.IsBlank)
        {
            return new AssociationValidityPeriodChange(null);
        }

        var period = AssociationValidityPeriod.TryCreate(input);
        if (period.IsFailure)
        {
            foreach (var (part, messages) in period.Error.FieldErrors)
            {
                fieldErrors[$"{PartyAssociationFields.AssociationValidFromToDate}.{part}"] = messages;
            }

            return null;
        }

        return new AssociationValidityPeriodChange(period.Value);
    }
}
