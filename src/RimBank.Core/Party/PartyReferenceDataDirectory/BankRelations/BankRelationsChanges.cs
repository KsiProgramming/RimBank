using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The changes a BIAN <c>BankRelations/Update</c> asks for: a partial update (roadmap 1.12, ADR 0035). A <c>null</c> change
/// keeps the current value; a change with a <c>null</c> value clears it. Adapters build it with <see cref="Create"/>.
/// </summary>
/// <param name="Type">The change of <c>BankRelationType</c>.</param>
/// <param name="BusinessUnitEmployee">The change of <c>BusinessUnitEmployeeReference.PartyReference</c>.</param>
/// <param name="StartDate">The change of <c>BankRelationReference.RelationshipStartDate</c>.</param>
/// <param name="EndDate">The change of <c>BankRelationReference.RelationshipEndDate</c>.</param>
/// <param name="LifecycleStatus">The change of <c>BankRelationReference.RelationshipLifecycleStatus</c>.</param>
public sealed record BankRelationsChanges(
    PartyReferenceChange<BankRelationType?>? Type = null,
    PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>? BusinessUnitEmployee = null,
    PartyReferenceChange<DateTimeOffset?>? StartDate = null,
    PartyReferenceChange<DateTimeOffset?>? EndDate = null,
    PartyReferenceChange<BankRelationLifecycleStatus?>? LifecycleStatus = null)
{
    /// <summary>No change at all: an Update that only records that the BankRelations was reviewed.</summary>
    public static BankRelationsChanges None { get; } = new();

    /// <summary>The changes that set every part to <paramref name="details"/>, clearing the unknown ones.</summary>
    /// <param name="details">The details to record.</param>
    /// <returns>The changes.</returns>
    public static BankRelationsChanges To(BankRelationsDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new BankRelationsChanges(
            new(details.Type),
            new(details.BusinessUnitEmployee),
            new(details.StartDate),
            new(details.EndDate),
            new(details.LifecycleStatus));
    }

    /// <summary>
    /// Reads the changes a caller sent. Fields and parts RimBank does not record are refused first, so nothing is updated
    /// partly. A blank value clears its part, and an empty <c>BankRelationReference</c> clears the dates and the status.
    /// </summary>
    /// <param name="input">The values sent: <c>null</c> keeps.</param>
    /// <param name="unsupportedFields">The BIAN paths of the fields and parts sent with a value that RimBank does not record.</param>
    /// <returns>
    /// The changes, <c>party.bank-relations-field-not-supported</c> naming every unsupported path, or
    /// <c>party.bank-relations-invalid</c> naming every invalid value.
    /// </returns>
    public static Result<BankRelationsChanges> Create(BankRelationsInput input, IReadOnlyCollection<string> unsupportedFields)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(unsupportedFields);
        if (unsupportedFields.Count > 0)
        {
            return BankRelationsErrors.BankRelationsFieldsNotSupported(unsupportedFields
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    field => field,
                    IReadOnlyList<string> (_) => ["RimBank does not record this field."],
                    StringComparer.Ordinal));
        }

        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var relationship = input.BankRelationReference switch
        {
            { IsEmpty: true } => new BankRelationReferenceInput(string.Empty, string.Empty, string.Empty),
            var given => given,
        };
        var changes = new BankRelationsChanges(
            Read(input.BankRelationType, BankRelationsFields.BankRelationType, BankRelationTypes.Read, fieldErrors),
            Read(input.BusinessUnitEmployeePartyReference, BankRelationsFields.BusinessUnitEmployeePartyReference, ReadEntry, fieldErrors),
            Read(relationship?.RelationshipStartDate, BankRelationsFields.RelationshipStartDateContent, ReadInstant, fieldErrors),
            Read(relationship?.RelationshipEndDate, BankRelationsFields.RelationshipEndDateContent, ReadInstant, fieldErrors),
            Read(relationship?.RelationshipLifecycleStatus, BankRelationsFields.RelationshipLifecycleStatusDescription, BankRelationLifecycleStatuses.Read, fieldErrors));

        return fieldErrors.Count switch
        {
            0 => changes,
            _ => BankRelationsErrors.BankRelationsInvalid(fieldErrors),
        };
    }

    // null keeps, blank clears, anything else is read; a value that cannot be read is named by its BIAN path.
    private static PartyReferenceChange<TValue?>? Read<TValue>(
        string? text,
        string field,
        Func<string, Result<TValue>> read,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
        where TValue : struct
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PartyReferenceChange<TValue?>(null);
        }

        var value = read(text);
        if (value.IsFailure)
        {
            fieldErrors[field] = value.Error.FieldErrors.Values.FirstOrDefault() ?? [value.Error.Message];
            return null;
        }

        return new PartyReferenceChange<TValue?>(value.Value);
    }

    private static Result<PartyReferenceDataDirectoryEntryId> ReadEntry(string text) =>
        PartyReferenceDataDirectoryEntryId.TryCreate(text.Trim()) switch
        {
            { IsFailure: true } => Error.Validation(
                BankRelationsErrors.BankRelationsInvalidCode,
                "The employee or unit must be the PartyReferenceDataDirectoryEntryId returned by Register."),
            var entry => entry,
        };

    private static Result<DateTimeOffset> ReadInstant(string text) =>
        PartyReferenceDates.TryReadUtcInstant(text, out var instant)
            ? instant
            : Error.Validation(
                BankRelationsErrors.BankRelationsInvalidCode,
                "The value must be an ISO 8601 date-time, for example 2024-01-01T00:00:00Z, or date.");
}

/// <summary>
/// The raw values an adapter received for a BIAN <c>BankRelations/Update</c> (roadmap 1.12). <see cref="BankRelationsChanges.Create"/>
/// reads them, so HTTP and MCP accept and refuse exactly the same values. <c>null</c> means not sent; an empty string means
/// sent blank, which clears the value.
/// </summary>
/// <param name="BankRelationType">BIAN <c>BankRelationType</c>.</param>
/// <param name="BusinessUnitEmployeePartyReference">BIAN <c>BusinessUnitEmployeeReference.PartyReference</c>.</param>
/// <param name="BankRelationReference">BIAN <c>BankRelationReference</c>, if sent.</param>
public sealed record BankRelationsInput(
    string? BankRelationType,
    string? BusinessUnitEmployeePartyReference,
    BankRelationReferenceInput? BankRelationReference);

/// <summary>The recorded parts of a BIAN <c>BankRelationReference</c> a caller sent, as raw text (roadmap 1.12).</summary>
/// <param name="RelationshipStartDate"><c>RelationshipStartDate.DateTimeContent</c>.</param>
/// <param name="RelationshipEndDate"><c>RelationshipEndDate.DateTimeContent</c>.</param>
/// <param name="RelationshipLifecycleStatus"><c>RelationshipLifecycleStatus.StatusDescription</c>.</param>
public sealed record BankRelationReferenceInput(
    string? RelationshipStartDate,
    string? RelationshipEndDate,
    string? RelationshipLifecycleStatus)
{
    /// <summary>Whether no part was sent: such an object clears the dates and the status.</summary>
    public bool IsEmpty => RelationshipStartDate is null && RelationshipEndDate is null && RelationshipLifecycleStatus is null;
}
