using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The name of a government-issued document, such as <c>Passport</c>: BIAN
/// <c>GovernmentIssuedDocumentDetails.DocumentReference.DocumentName</c> (roadmap 1.20, ADR 0032). It is trimmed, not
/// blank and at most 140 characters (ISO 20022 Max140Text), free text.
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<GovernmentIssuedDocumentName>))]
public sealed record GovernmentIssuedDocumentName : IPartyReferenceText<GovernmentIssuedDocumentName>
{
    /// <summary>ISO 20022 Max140Text.</summary>
    public const int MaxLength = 140;

    private GovernmentIssuedDocumentName(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<GovernmentIssuedDocumentName> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "document name").Map(value => new GovernmentIssuedDocumentName(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
