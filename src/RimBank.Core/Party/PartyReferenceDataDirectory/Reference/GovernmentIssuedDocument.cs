using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A government-issued document the party declares, such as a passport or a driving licence: BIAN Reference
/// <c>GovernmentIssuedDocumentDetails</c>, a <c>PartyDocument</c> (roadmap 1.20, ADR 0032). RimBank records its type, its
/// number, its name and its validity period, enough to identify the document and see whether it has expired. Whether the
/// document is genuine is a KYC assessment the directory does not record. Personal data (7.2).
/// </summary>
/// <param name="Type">BIAN <c>PartyDocumentType</c>, required.</param>
/// <param name="Number">BIAN <c>DocumentReference.DocumentIdentification.IdentifierValue</c>, required.</param>
/// <param name="Name">BIAN <c>DocumentReference.DocumentName</c>, for example <c>Passport</c>.</param>
/// <param name="ValidFrom">BIAN <c>DocumentReference.DocumentValidityPeriod.FromDateTime.DateTimeContent</c>, as a date.</param>
/// <param name="ValidTo">
/// BIAN <c>DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent</c>, as a date, not before
/// <paramref name="ValidFrom"/>. An expired document can be recorded.
/// </param>
public sealed record GovernmentIssuedDocument(
    PartyDocumentType Type,
    GovernmentIssuedDocumentNumber Number,
    GovernmentIssuedDocumentName? Name,
    DateOnly? ValidFrom,
    DateOnly? ValidTo)
{
    /// <summary>BIAN <c>PartyDocumentType</c>.</summary>
    public const string PartyDocumentTypePart = "PartyDocumentType";

    /// <summary>BIAN <c>DocumentReference</c>, a <c>Document</c> object.</summary>
    public const string DocumentReferencePart = "DocumentReference";

    /// <summary>BIAN <c>DocumentReference.DocumentIdentification</c>, an <c>Identifier</c> object.</summary>
    public const string DocumentIdentificationPart = DocumentReferencePart + ".DocumentIdentification";

    /// <summary>BIAN <c>DocumentReference.DocumentIdentification.IdentifierValue</c>: the document number.</summary>
    public const string IdentifierValuePart = DocumentIdentificationPart + ".IdentifierValue";

    /// <summary>BIAN <c>DocumentReference.DocumentName</c>.</summary>
    public const string DocumentNamePart = DocumentReferencePart + ".DocumentName";

    /// <summary>BIAN <c>DocumentReference.DocumentValidityPeriod</c>, a <c>Datetimeperiod</c> object.</summary>
    public const string ValidityPeriodPart = DocumentReferencePart + ".DocumentValidityPeriod";

    /// <summary>BIAN <c>DocumentReference.DocumentValidityPeriod.FromDateTime</c>, a <c>Datetime</c> object.</summary>
    public const string FromDateTimePart = ValidityPeriodPart + ".FromDateTime";

    /// <summary>BIAN <c>DocumentReference.DocumentValidityPeriod.ToDateTime</c>, a <c>Datetime</c> object.</summary>
    public const string ToDateTimePart = ValidityPeriodPart + ".ToDateTime";

    /// <summary>The part of a BIAN <c>Datetime</c> object that holds the date.</summary>
    public const string DateTimeContentPart = "DateTimeContent";

    /// <summary>BIAN <c>…FromDateTime.DateTimeContent</c>: the first day of validity.</summary>
    public const string ValidFromPart = FromDateTimePart + "." + DateTimeContentPart;

    /// <summary>BIAN <c>…ToDateTime.DateTimeContent</c>: the last day of validity.</summary>
    public const string ValidToPart = ToDateTimePart + "." + DateTimeContentPart;

    /// <summary>
    /// Reads a document a caller sent whose parts are not all blank (<see cref="PartyDocumentInput.IsBlank"/>): trims every
    /// part, drops the blank optional ones, and checks the type, the number, the name and the validity dates.
    /// </summary>
    /// <param name="input">The parts sent.</param>
    /// <returns>
    /// The document, or <c>party.reference-invalid</c> naming every invalid part by its BIAN path relative to the
    /// document (for example <c>DocumentReference.DocumentIdentification.IdentifierValue</c>).
    /// </returns>
    public static Result<GovernmentIssuedDocument> TryCreate(PartyDocumentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var type = string.IsNullOrWhiteSpace(input.PartyDocumentType)
            ? PartyReferenceErrors.ValueInvalid(
                "The PartyDocumentType is required: WorkDocument, CertificationDocument or PersonalDocument.")
            : PartyDocumentTypes.Read(input.PartyDocumentType);
        Collect(type, PartyDocumentTypePart, fieldErrors);

        var number = string.IsNullOrWhiteSpace(input.DocumentNumber)
            ? PartyReferenceErrors.ValueInvalid("The document number is required.")
            : GovernmentIssuedDocumentNumber.TryCreate(input.DocumentNumber);
        Collect(number, IdentifierValuePart, fieldErrors);

        var name = string.IsNullOrWhiteSpace(input.DocumentName) ? null : GovernmentIssuedDocumentName.TryCreate(input.DocumentName);
        if (name is not null)
        {
            Collect(name, DocumentNamePart, fieldErrors);
        }

        var validFrom = ReadDate(input.ValidFrom, ValidFromPart, fieldErrors);
        var validTo = ReadDate(input.ValidTo, ValidToPart, fieldErrors);
        if (validFrom is { } from && validTo is { } to && to < from)
        {
            fieldErrors[ValidToPart] = ["The end of the validity period must not be before its start."];
        }

        return fieldErrors.Count switch
        {
            0 => new GovernmentIssuedDocument(type.Value, number.Value, name?.Value, validFrom, validTo),
            _ => PartyReferenceErrors.ReferenceInvalid(fieldErrors),
        };
    }

    private static void Collect<T>(Result<T> result, string part, Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (result.IsFailure)
        {
            fieldErrors[part] = [result.Error.Message];
        }
    }

    private static DateOnly? ReadDate(string? text, string part, Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (PartyReferenceDates.TryReadDate(text, out var date))
        {
            return date;
        }

        fieldErrors[part] = ["The date must be an ISO 8601 date, for example 2033-03-31, or date-time."];
        return null;
    }
}
