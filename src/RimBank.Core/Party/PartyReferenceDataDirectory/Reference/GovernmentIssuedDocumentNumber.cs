using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The number of a government-issued document, such as a passport number: BIAN
/// <c>GovernmentIssuedDocumentDetails.DocumentReference.DocumentIdentification.IdentifierValue</c> (roadmap 1.20,
/// ADR 0032). It is trimmed, not blank and at most 35 characters (ISO 20022 Max35Text), with no format check per country.
/// Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<GovernmentIssuedDocumentNumber>))]
public sealed record GovernmentIssuedDocumentNumber : IPartyReferenceText<GovernmentIssuedDocumentNumber>
{
    /// <summary>ISO 20022 Max35Text.</summary>
    public const int MaxLength = 35;

    private GovernmentIssuedDocumentNumber(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<GovernmentIssuedDocumentNumber> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "document number").Map(value => new GovernmentIssuedDocumentNumber(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
