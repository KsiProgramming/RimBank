using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The changes a BIAN <c>Reference/Update</c> asks for: a partial update of the Reference (ADR 0024, ADR 0029). For each
/// value, <c>null</c> keeps the current one, a change with no value clears it, and a change with a value sets it.
/// Adapters build it with <see cref="Create"/>, which checks the values and refuses the fields RimBank does not record yet.
/// </summary>
/// <param name="TaxIdentificationNumber">The change of the tax identification number.</param>
/// <param name="NationalRegistrationNumber">The change of the national registration number.</param>
/// <param name="LegalEntityIdentifier">The change of the LEI.</param>
/// <param name="PartyFullLegalName">The change of the full legal name (1.18).</param>
/// <param name="PartyNameSalutation">The change of the salutation (1.18).</param>
/// <param name="DateOfBirth">The change of the date of birth (1.18).</param>
/// <param name="Nationality">The change of the nationality (1.18).</param>
/// <param name="PartyCountryOfResidence">The change of the country of residence (1.18).</param>
/// <param name="ResidencyStatus">The change of the residency status (1.18).</param>
public sealed record PartyReferenceChanges(
    PartyIdentifierChange<TaxIdentificationNumber>? TaxIdentificationNumber,
    PartyIdentifierChange<NationalRegistrationNumber>? NationalRegistrationNumber,
    PartyIdentifierChange<LegalEntityIdentifier>? LegalEntityIdentifier,
    PartyReferenceChange<PartyFullLegalName?>? PartyFullLegalName = null,
    PartyReferenceChange<PartyNameSalutation?>? PartyNameSalutation = null,
    PartyReferenceChange<DateOnly?>? DateOfBirth = null,
    PartyReferenceChange<CountryCode?>? Nationality = null,
    PartyReferenceChange<CountryCode?>? PartyCountryOfResidence = null,
    PartyReferenceChange<ResidencyStatus?>? ResidencyStatus = null)
{
    private const string CountryMessage = "The country must be an ISO 3166-1 alpha-2 code, for example FR.";

    /// <summary>No change at all: an Update that only records that the Reference was reviewed.</summary>
    public static PartyReferenceChanges None { get; } = new(null, null, null);

    /// <summary>
    /// Reads the changes a caller sent. Every adapter (HTTP, MCP) builds the changes here, so they accept and refuse
    /// exactly the same values. Fields RimBank does not record yet are refused first, so nothing is updated partly.
    /// Whether a date of birth is after today is the decision's check, because it needs the handler's clock.
    /// </summary>
    /// <param name="input">The values sent: <c>null</c> keeps, blank clears.</param>
    /// <param name="unsupportedFields">
    /// The BIAN names of the other Reference fields the caller sent with a value (for example <c>eMailAddress</c>,
    /// <c>GovernmentIssuedIdentityReference.OtherIdentification</c> or <c>Nationality.Citizen</c>).
    /// </param>
    /// <returns>
    /// The changes, <c>party.reference-field-not-supported</c> naming every unsupported field and the phase that will
    /// record it, or <c>party.reference-invalid</c> naming every invalid value.
    /// </returns>
    public static Result<PartyReferenceChanges> Create(PartyReferenceInput input, IReadOnlyCollection<string> unsupportedFields)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(unsupportedFields);
        if (unsupportedFields.Count > 0)
        {
            return PartyReferenceErrors.ReferenceFieldsNotSupported(unsupportedFields
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(field => field, NotSupportedMessages, StringComparer.Ordinal));
        }

        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var changes = new PartyReferenceChanges(
            ReadIdentifier<TaxIdentificationNumber>(input.TaxIdentificationNumber, PartyReferenceFields.TaxIdentificationNumber, fieldErrors),
            ReadIdentifier<NationalRegistrationNumber>(input.NationalRegistrationNumber, PartyReferenceFields.NationalRegistrationNumber, fieldErrors),
            ReadIdentifier<LegalEntityIdentifier>(input.LegalEntityIdentifier, PartyReferenceFields.LegalEntityIdentifier, fieldErrors),
            ReadText<PartyFullLegalName>(input.PartyFullLegalName, PartyReferenceFields.PartyFullLegalName, fieldErrors),
            ReadText<PartyNameSalutation>(input.PartyNameSalutation, PartyReferenceFields.PartyNameSalutation, fieldErrors),
            ReadDateOfBirth(input.DateofBirth, fieldErrors),
            ReadCountry(input.Nationality, PartyReferenceFields.NationalityCode, fieldErrors),
            ReadCountry(input.PartyCountryOfResidence, PartyReferenceFields.PartyCountryOfResidenceCode, fieldErrors),
            ReadResidencyStatus(input.ResidencyStatus, fieldErrors));

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

    private static PartyIdentifierChange<TIdentifier>? ReadIdentifier<TIdentifier>(
        string? text,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
        where TIdentifier : class, IPartyIdentifier<TIdentifier> =>
        ReadText<TIdentifier>(text, field, fieldErrors) switch
        {
            null => null,
            var change => new PartyIdentifierChange<TIdentifier>(change.Value),
        };

    private static PartyReferenceChange<TText?>? ReadText<TText>(
        string? text,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
        where TText : class, IPartyReferenceText<TText>
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PartyReferenceChange<TText?>(null);
        }

        var value = TText.TryCreate(text);
        if (value.IsFailure)
        {
            fieldErrors[field] = [value.Error.Message];
            return null;
        }

        return new PartyReferenceChange<TText?>(value.Value);
    }

    private static PartyReferenceChange<DateOnly?>? ReadDateOfBirth(string? text, Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PartyReferenceChange<DateOnly?>(null);
        }

        if (!PartyReferenceDates.TryReadDate(text, out var date))
        {
            fieldErrors[PartyReferenceFields.DateofBirth] =
                ["The date of birth must be an ISO 8601 date, for example 1990-05-17, or date-time."];
            return null;
        }

        return new PartyReferenceChange<DateOnly?>(date);
    }

    private static PartyReferenceChange<CountryCode?>? ReadCountry(
        PartyCountryInput? country,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (country is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(country.Code))
        {
            return new PartyReferenceChange<CountryCode?>(null);
        }

        var code = CountryCode.TryCreate(country.Code.Trim().ToUpperInvariant());
        if (code.IsFailure)
        {
            fieldErrors[field] = [CountryMessage];
            return null;
        }

        return new PartyReferenceChange<CountryCode?>(code.Value);
    }

    private static PartyReferenceChange<ResidencyStatus?>? ReadResidencyStatus(
        ResidencyStatusInput? status,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (status is null)
        {
            return null;
        }

        var (description, dateTime) = (
            string.IsNullOrWhiteSpace(status.StatusDescription) ? null : status.StatusDescription.Trim(),
            string.IsNullOrWhiteSpace(status.StatusDateTime) ? null : status.StatusDateTime);
        if (description is null && dateTime is null)
        {
            return new PartyReferenceChange<ResidencyStatus?>(null);
        }

        var code = description?.ToUpperInvariant() switch
        {
            "RESIDENT" => ResidencyStatusCode.Resident,
            "NONRESIDENT" => ResidencyStatusCode.NonResident,
            _ => (ResidencyStatusCode?)null,
        };
        if (code is null)
        {
            fieldErrors[PartyReferenceFields.ResidencyStatusDescription] = description switch
            {
                null => ["The residency status is required when its date-time is sent: Resident or NonResident."],
                _ => ["The residency status must be Resident or NonResident."],
            };
        }

        DateTimeOffset? assigned = null;
        if (dateTime is not null)
        {
            if (PartyReferenceDates.TryReadDateTime(dateTime, out var parsed))
            {
                assigned = parsed;
            }
            else
            {
                fieldErrors[PartyReferenceFields.ResidencyStatusDateTime] =
                    ["The status date-time must be an ISO 8601 date-time, for example 2024-01-01T00:00:00Z."];
            }
        }

        return code is { } known ? new PartyReferenceChange<ResidencyStatus?>(new ResidencyStatus(known, assigned)) : null;
    }
}
