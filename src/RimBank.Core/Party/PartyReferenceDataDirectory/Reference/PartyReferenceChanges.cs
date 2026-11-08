using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The changes a BIAN <c>Reference/Update</c> asks for: a partial update of the party identifiers (ADR 0024, ADR 0029).
/// For each identifier, <c>null</c> keeps the current one, a change with no value clears it, and a change with a value
/// sets it. Adapters build it with <see cref="Create"/>, which checks the identifiers and refuses the fields RimBank
/// does not record yet.
/// </summary>
/// <param name="TaxIdentificationNumber">The change of the tax identification number.</param>
/// <param name="NationalRegistrationNumber">The change of the national registration number.</param>
/// <param name="LegalEntityIdentifier">The change of the LEI.</param>
public sealed record PartyReferenceChanges(
    PartyIdentifierChange<TaxIdentificationNumber>? TaxIdentificationNumber,
    PartyIdentifierChange<NationalRegistrationNumber>? NationalRegistrationNumber,
    PartyIdentifierChange<LegalEntityIdentifier>? LegalEntityIdentifier)
{
    /// <summary>No change at all: an Update that only records that the identifiers were reviewed.</summary>
    public static PartyReferenceChanges None { get; } = new(null, null, null);

    /// <summary>
    /// Reads the changes a caller sent. Every adapter (HTTP, MCP) builds the changes here, so they accept and refuse
    /// exactly the same values. Fields RimBank does not record yet are refused first, so nothing is updated partly.
    /// </summary>
    /// <param name="taxIdentificationNumber">The tax identification number sent: <c>null</c> keeps, blank clears.</param>
    /// <param name="nationalRegistrationNumber">The national registration number sent: <c>null</c> keeps, blank clears.</param>
    /// <param name="legalEntityIdentifier">The LEI sent: <c>null</c> keeps, blank clears.</param>
    /// <param name="unsupportedFields">
    /// The BIAN names of the other Reference fields the caller sent with a value (for example <c>DateofBirth</c> or
    /// <c>GovernmentIssuedIdentityReference.OtherIdentification</c>).
    /// </param>
    /// <returns>
    /// The changes, <c>party.reference-field-not-supported</c> naming every unsupported field and the phase that will
    /// record it, or <c>party.reference-invalid</c> naming every invalid identifier.
    /// </returns>
    public static Result<PartyReferenceChanges> Create(
        string? taxIdentificationNumber,
        string? nationalRegistrationNumber,
        string? legalEntityIdentifier,
        IReadOnlyCollection<string> unsupportedFields)
    {
        ArgumentNullException.ThrowIfNull(unsupportedFields);
        if (unsupportedFields.Count > 0)
        {
            return PartyReferenceErrors.ReferenceFieldsNotSupported(unsupportedFields
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(field => field, NotSupportedMessages, StringComparer.Ordinal));
        }

        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var changes = new PartyReferenceChanges(
            Read<TaxIdentificationNumber>(taxIdentificationNumber, PartyReferenceFields.TaxIdentificationNumber, fieldErrors),
            Read<NationalRegistrationNumber>(nationalRegistrationNumber, PartyReferenceFields.NationalRegistrationNumber, fieldErrors),
            Read<LegalEntityIdentifier>(legalEntityIdentifier, PartyReferenceFields.LegalEntityIdentifier, fieldErrors));

        return fieldErrors.Count switch
        {
            0 => changes,
            _ => PartyReferenceErrors.ReferenceInvalid(fieldErrors),
        };
    }

    private static IReadOnlyList<string> NotSupportedMessages(string field) =>
        PartyReferenceFields.Staged.TryGetValue(field, out var phase)
            ? [$"RimBank does not record this field yet; roadmap phase {phase} adds it."]
            : ["RimBank does not record this field."];

    private static PartyIdentifierChange<TIdentifier>? Read<TIdentifier>(
        string? text,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
        where TIdentifier : class, IPartyIdentifier<TIdentifier>
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PartyIdentifierChange<TIdentifier>(null);
        }

        var identifier = TIdentifier.TryCreate(text);
        if (identifier.IsFailure)
        {
            fieldErrors[field] = [identifier.Error.Message];
            return null;
        }

        return new PartyIdentifierChange<TIdentifier>(identifier.Value);
    }
}
