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
/// <param name="ResidentialAddress">The change of the residential address (1.19).</param>
/// <param name="PartyAddress">The change of the party's postal address (1.19).</param>
/// <param name="CorporateAddress">The change of the company's registered address (1.19).</param>
/// <param name="EmailAddress">The change of the email address (1.19).</param>
/// <param name="MobileNumber">The change of the mobile number (1.19).</param>
/// <param name="SocialNetworkAddress">The change of the social network profile (1.19).</param>
/// <param name="PartyLegalStructure">The change of the legal structure (1.20).</param>
/// <param name="PoliticalExposureType">The change of the declared political exposure (1.20).</param>
/// <param name="PoliticalExposureDescription">The change of the political exposure description (1.20).</param>
/// <param name="CustomerSinceDate">The change of the customer-since date (1.20).</param>
/// <param name="GovernmentIssuedDocument">The change of the government-issued document (1.20).</param>
/// <param name="DocumentDirectoryEntry">The change of the document directory reference (1.20).</param>
/// <param name="CorporateCustomer">The change of the corporate customer reference (1.21).</param>
/// <param name="LegalEntity">The change of the legal entity reference (1.21).</param>
/// <param name="CompanyOfficer">The change of the company officer reference (1.21).</param>
/// <param name="CompanyOfficerRole">The change of the company officer's role (1.21).</param>
public sealed record PartyReferenceChanges(
    PartyIdentifierChange<TaxIdentificationNumber>? TaxIdentificationNumber,
    PartyIdentifierChange<NationalRegistrationNumber>? NationalRegistrationNumber,
    PartyIdentifierChange<LegalEntityIdentifier>? LegalEntityIdentifier,
    PartyReferenceChange<PartyFullLegalName?>? PartyFullLegalName = null,
    PartyReferenceChange<PartyNameSalutation?>? PartyNameSalutation = null,
    PartyReferenceChange<DateOnly?>? DateOfBirth = null,
    PartyReferenceChange<CountryCode?>? Nationality = null,
    PartyReferenceChange<CountryCode?>? PartyCountryOfResidence = null,
    PartyReferenceChange<ResidencyStatus?>? ResidencyStatus = null,
    PartyReferenceChange<PostalAddress?>? ResidentialAddress = null,
    PartyReferenceChange<PostalAddress?>? PartyAddress = null,
    PartyReferenceChange<PostalAddress?>? CorporateAddress = null,
    PartyReferenceChange<EmailAddress?>? EmailAddress = null,
    PartyReferenceChange<MobileNumber?>? MobileNumber = null,
    PartyReferenceChange<SocialNetworkAddress?>? SocialNetworkAddress = null,
    PartyReferenceChange<PartyLegalStructure?>? PartyLegalStructure = null,
    PartyReferenceChange<PoliticalExposureType?>? PoliticalExposureType = null,
    PartyReferenceChange<PoliticalExposureDescription?>? PoliticalExposureDescription = null,
    PartyReferenceChange<DateOnly?>? CustomerSinceDate = null,
    PartyReferenceChange<GovernmentIssuedDocument?>? GovernmentIssuedDocument = null,
    PartyReferenceChange<DocumentDirectoryEntryReference?>? DocumentDirectoryEntry = null,
    PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>? CorporateCustomer = null,
    PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>? LegalEntity = null,
    PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>? CompanyOfficer = null,
    PartyReferenceChange<CompanyOfficerRole?>? CompanyOfficerRole = null)
{
    /// <summary>
    /// The directory entries this Update references (1.21, ADR 0034), each with the BIAN path that names it: the ones it
    /// sets, never the ones it keeps or clears. The handler loads them through the write port so the decision can check
    /// them.
    /// </summary>
    /// <returns>The BIAN path and the entry id of each reference set, in BIAN order.</returns>
    public IReadOnlyList<(string Field, PartyReferenceDataDirectoryEntryId EntryId)> ReferencedEntries() =>
    [
        .. Referenced(PartyReferenceFields.CorporateCustomerPartyReference, CorporateCustomer),
        .. Referenced(PartyReferenceFields.LegalEntityPartyReference, LegalEntity),
        .. Referenced(PartyReferenceFields.CompanyOfficerPartyReference, CompanyOfficer),
    ];

    /// <summary>
    /// The earliest date of birth accepted (inclusive, ADR 0029): an earlier one is almost always a typo. The latest is
    /// today, checked by the decision because it needs the handler's clock.
    /// </summary>
    public static DateOnly EarliestDateOfBirth { get; } = new(1900, 1, 1);

    /// <summary>
    /// The earliest customer-since date accepted (inclusive, ADR 0032), for the same reason as
    /// <see cref="EarliestDateOfBirth"/>. The latest is today, checked by the decision.
    /// </summary>
    public static DateOnly EarliestCustomerSinceDate { get; } = new(1900, 1, 1);

    /// <summary>No change at all: an Update that only records that the Reference was reviewed.</summary>
    public static PartyReferenceChanges None { get; } = new(null, null, null);

    /// <summary>
    /// Reads the changes a caller sent. Every adapter (HTTP, MCP) builds the changes here, so they accept and refuse
    /// exactly the same values. Fields RimBank does not record yet are refused first, so nothing is updated partly.
    /// A date of birth before <see cref="EarliestDateOfBirth"/> is refused here; whether it is after today is the
    /// decision's check, because it needs the handler's clock.
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
            ReadDate(input.DateofBirth, PartyReferenceFields.DateofBirth, "date of birth", EarliestDateOfBirth, fieldErrors),
            ReadCountry(input.Nationality, PartyReferenceFields.NationalityCode, fieldErrors),
            ReadCountry(input.PartyCountryOfResidence, PartyReferenceFields.PartyCountryOfResidenceCode, fieldErrors),
            ReadResidencyStatus(input.ResidencyStatus, fieldErrors),
            ReadAddress(input.ResidentialAddress, PartyReferenceFields.ResidentialAddress, fieldErrors),
            ReadAddress(input.PartyAddress, PartyReferenceFields.PartyAddress, fieldErrors),
            ReadAddress(input.CorporateAddress, PartyReferenceFields.CorporateAddress, fieldErrors),
            ReadText<EmailAddress>(input.EmailAddress, PartyReferenceFields.EMailAddressEmailAddress, fieldErrors),
            ReadText<MobileNumber>(input.MobileNumber, PartyReferenceFields.CellPhoneNumberMobileNumber, fieldErrors),
            ReadText<SocialNetworkAddress>(input.SocialNetworkUrl, PartyReferenceFields.SocialNetworkContactsUrlAddress, fieldErrors),
            ReadCode(input.PartyLegalStructure, PartyLegalStructures.Read, PartyReferenceFields.PartyLegalStructure, fieldErrors),
            ReadCode(input.PoliticalExposureType, PoliticalExposureTypes.Read, PartyReferenceFields.PoliticalExposureType, fieldErrors),
            ReadText<PoliticalExposureDescription>(
                input.PoliticalExposureDescription, PartyReferenceFields.PoliticalExposureDescriptionRecord, fieldErrors),
            ReadDate(
                input.CustomerSinceDate,
                PartyReferenceFields.CustomerSinceDate,
                "customer-since date",
                EarliestCustomerSinceDate,
                fieldErrors),
            ReadDocument(input.GovernmentIssuedDocument, fieldErrors),
            ReadText<DocumentDirectoryEntryReference>(
                input.DocumentDirectoryEntryIdentification, PartyReferenceFields.DocumentDirectoryEntryIdentification, fieldErrors),
            ReadEntry(input.CorporateCustomerReference, PartyReferenceFields.CorporateCustomerPartyReference, fieldErrors),
            ReadEntry(input.CorporateCustomerLegalEntityReference, PartyReferenceFields.LegalEntityPartyReference, fieldErrors),
            ReadEntry(input.CompanyOfficerReference, PartyReferenceFields.CompanyOfficerPartyReference, fieldErrors),
            ReadText<CompanyOfficerRole>(input.CompanyOfficerRole, PartyReferenceFields.CompanyOfficerRole, fieldErrors));

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

    private static PartyReferenceChange<DateOnly?>? ReadDate(
        string? text,
        string field,
        string name,
        DateOnly earliest,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
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
            fieldErrors[field] = [$"The {name} must be an ISO 8601 date, for example 1990-05-17, or date-time."];
            return null;
        }

        if (date < earliest)
        {
            fieldErrors[field] = [$"The {name} must not be before {earliest:yyyy-MM-dd}."];
            return null;
        }

        return new PartyReferenceChange<DateOnly?>(date);
    }

    private static PartyReferenceChange<TCode?>? ReadCode<TCode>(
        string? text,
        Func<string, Result<TCode>> read,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
        where TCode : struct, Enum
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PartyReferenceChange<TCode?>(null);
        }

        var code = read(text);
        if (code.IsFailure)
        {
            fieldErrors[field] = [code.Error.Message];
            return null;
        }

        return new PartyReferenceChange<TCode?>(code.Value);
    }

    private static IEnumerable<(string Field, PartyReferenceDataDirectoryEntryId EntryId)> Referenced(
        string field,
        PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>? change) =>
        change?.Value switch
        {
            { } entryId => [(field, entryId)],
            null => [],
        };

    private static PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>? ReadEntry(
        string? text,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>(null);
        }

        var entryId = PartyReferenceDataDirectoryEntryId.TryCreate(text.Trim());
        if (entryId.IsFailure)
        {
            fieldErrors[field] = ["The party reference must be the PartyReferenceDataDirectoryEntryId returned by Register."];
            return null;
        }

        return new PartyReferenceChange<PartyReferenceDataDirectoryEntryId?>(entryId.Value);
    }

    private static PartyReferenceChange<GovernmentIssuedDocument?>? ReadDocument(
        PartyDocumentInput? document,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (document is null)
        {
            return null;
        }

        if (document.IsBlank)
        {
            return new PartyReferenceChange<GovernmentIssuedDocument?>(null);
        }

        var read = PartyReferenceDataDirectory.GovernmentIssuedDocument.TryCreate(document);
        if (read.IsFailure)
        {
            foreach (var (part, messages) in read.Error.FieldErrors)
            {
                fieldErrors[$"{PartyReferenceFields.GovernmentIssuedDocumentDetails}.{part}"] = messages;
            }

            return null;
        }

        return new PartyReferenceChange<GovernmentIssuedDocument?>(read.Value);
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

        var code = PartyReferenceCountry.Read(country.Code);
        if (code.IsFailure)
        {
            fieldErrors[field] = [code.Error.Message];
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

    private static PartyReferenceChange<PostalAddress?>? ReadAddress(
        PostalAddressInput? address,
        string field,
        Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsBlank)
        {
            return new PartyReferenceChange<PostalAddress?>(null);
        }

        var read = PostalAddress.TryCreate(address);
        if (read.IsFailure)
        {
            foreach (var (part, messages) in read.Error.FieldErrors)
            {
                fieldErrors[$"{field}.{part}"] = messages;
            }

            return null;
        }

        return new PartyReferenceChange<PostalAddress?>(read.Value);
    }
}
