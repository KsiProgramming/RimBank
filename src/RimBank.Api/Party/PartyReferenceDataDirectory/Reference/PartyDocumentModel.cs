using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's Reference <c>GovernmentIssuedDocumentDetails</c> (a <c>PartyDocument</c>), in requests and responses. RimBank
/// records its <c>PartyDocumentType</c> and, in its <c>DocumentReference</c>, the document number, name and validity
/// period (roadmap 1.20, ADR 0032); a request that sends any other part of it, at any depth, is refused.
/// </summary>
public sealed record PartyDocumentModel
{
    /// <summary>BIAN <c>PartyDocumentType</c>: <c>WorkDocument</c>, <c>CertificationDocument</c> or <c>PersonalDocument</c>.</summary>
    [JsonPropertyName("PartyDocumentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PartyDocumentType { get; init; }

    /// <summary>BIAN <c>DocumentReference</c>: the document itself.</summary>
    [JsonPropertyName("DocumentReference")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DocumentModel? DocumentReference { get; init; }

    /// <summary>The other parts a request sent (for example <c>PartyReference</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>The model of a recorded document.</summary>
    /// <param name="document">The document, or <c>null</c>.</param>
    /// <returns>The model, or <c>null</c> when no document is recorded.</returns>
    public static PartyDocumentModel? From(GovernmentIssuedDocument? document) =>
        document switch
        {
            null => null,
            _ => new PartyDocumentModel
            {
                PartyDocumentType = document.Type.ToString(),
                DocumentReference = new DocumentModel
                {
                    DocumentIdentification = new IdentifierModel { IdentifierValue = document.Number.Value },
                    DocumentName = document.Name?.Value,
                    DocumentValidityPeriod = (document.ValidFrom, document.ValidTo) switch
                    {
                        (null, null) => null,
                        var (from, to) => new DateTimePeriodModel { FromDateTime = DateOf(from), ToDateTime = DateOf(to) },
                    },
                },
            },
        };

    /// <summary>The parts of this document as the core reads them.</summary>
    /// <returns>The input.</returns>
    public PartyDocumentInput ToInput() =>
        new(
            PartyDocumentType,
            DocumentReference?.DocumentIdentification?.IdentifierValue,
            DocumentReference?.DocumentName,
            DocumentReference?.DocumentValidityPeriod?.FromDateTime?.DateTimeContent,
            DocumentReference?.DocumentValidityPeriod?.ToDateTime?.DateTimeContent);

    /// <summary>
    /// The BIAN paths, relative to <paramref name="field"/>, of every part this document sends with a value that RimBank
    /// does not record.
    /// </summary>
    /// <param name="field">The BIAN field that holds the document.</param>
    /// <returns>The paths, for example <c>GovernmentIssuedDocumentDetails.DocumentReference.DocumentStatus</c>.</returns>
    public IEnumerable<string> UnsupportedParts(string field) =>
        ReferenceModelParts.Present(field, Other)
            .Concat(ReferenceModelParts.Present($"{field}.{GovernmentIssuedDocument.DocumentReferencePart}", DocumentReference?.Other))
            .Concat(ReferenceModelParts.Present(
                $"{field}.{GovernmentIssuedDocument.DocumentIdentificationPart}", DocumentReference?.DocumentIdentification?.Other))
            .Concat(ReferenceModelParts.Present(
                $"{field}.{GovernmentIssuedDocument.ValidityPeriodPart}", DocumentReference?.DocumentValidityPeriod?.Other))
            .Concat(ReferenceModelParts.Present(
                $"{field}.{GovernmentIssuedDocument.FromDateTimePart}", DocumentReference?.DocumentValidityPeriod?.FromDateTime?.Other))
            .Concat(ReferenceModelParts.Present(
                $"{field}.{GovernmentIssuedDocument.ToDateTimePart}", DocumentReference?.DocumentValidityPeriod?.ToDateTime?.Other));

    private static DateTimeModel? DateOf(DateOnly? date) =>
        date switch
        {
            null => null,
            { } day => new DateTimeModel { DateTimeContent = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
        };
}

/// <summary>BIAN's <c>Document</c> object, inside <see cref="PartyDocumentModel"/> (roadmap 1.20).</summary>
public sealed record DocumentModel
{
    /// <summary>BIAN <c>DocumentIdentification</c>: the document number in its <c>IdentifierValue</c>.</summary>
    [JsonPropertyName("DocumentIdentification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IdentifierModel? DocumentIdentification { get; init; }

    /// <summary>BIAN <c>DocumentName</c>, for example <c>Passport</c> (Max140Text).</summary>
    [JsonPropertyName("DocumentName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentName { get; init; }

    /// <summary>BIAN <c>DocumentValidityPeriod</c>: the first and last day of validity.</summary>
    [JsonPropertyName("DocumentValidityPeriod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimePeriodModel? DocumentValidityPeriod { get; init; }

    /// <summary>The other parts a request sent (for example <c>DocumentStatus</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}

/// <summary>BIAN's <c>Identifier</c> object, inside <see cref="DocumentModel"/> (roadmap 1.20).</summary>
public sealed record IdentifierModel
{
    /// <summary>BIAN <c>IdentifierValue</c>: the document number (Max35Text).</summary>
    [JsonPropertyName("IdentifierValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdentifierValue { get; init; }

    /// <summary>The other parts a request sent (for example <c>IdentifierIssuerReference</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}

/// <summary>BIAN's <c>Datetimeperiod</c> object, inside <see cref="DocumentModel"/> (roadmap 1.20).</summary>
public sealed record DateTimePeriodModel
{
    /// <summary>BIAN <c>FromDateTime</c>: the first day of validity.</summary>
    [JsonPropertyName("FromDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeModel? FromDateTime { get; init; }

    /// <summary>BIAN <c>ToDateTime</c>: the last day of validity.</summary>
    [JsonPropertyName("ToDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeModel? ToDateTime { get; init; }

    /// <summary>The other parts a request sent, which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}

/// <summary>BIAN's <c>Datetime</c> object, inside <see cref="DateTimePeriodModel"/> (roadmap 1.20).</summary>
public sealed record DateTimeModel
{
    /// <summary>BIAN <c>DateTimeContent</c>: an ISO 8601 date (answered as <c>YYYY-MM-DD</c>) or date-time.</summary>
    [JsonPropertyName("DateTimeContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DateTimeContent { get; init; }

    /// <summary>The other parts a request sent (for example <c>TimeZoneCode</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
