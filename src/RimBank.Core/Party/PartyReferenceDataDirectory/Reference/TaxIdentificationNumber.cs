using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The number a tax authority assigned to the party: BIAN <c>GovernmentIssuedIdentityReference.TaxIdentificationNumber</c>
/// (ISO 20022 Max35Text). It is trimmed, and its country format is not checked (ADR 0029). Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyIdentifierJsonConverter<TaxIdentificationNumber>))]
public sealed record TaxIdentificationNumber : IPartyIdentifier<TaxIdentificationNumber>
{
    /// <summary>ISO 20022 Max35Text.</summary>
    public const int MaxLength = 35;

    private TaxIdentificationNumber(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<TaxIdentificationNumber> TryCreate(string? text) =>
        PartyIdentifierText.Read(text, MaxLength, "tax identification number").Map(value => new TaxIdentificationNumber(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
