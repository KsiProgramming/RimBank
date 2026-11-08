using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The number a national registration authority assigned to the party (for example a national identity number):
/// BIAN <c>GovernmentIssuedIdentityReference.NationalRegistrationNumber</c> (ISO 20022 Max35Text). It is trimmed, and its
/// country format is not checked (ADR 0029). Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<NationalRegistrationNumber>))]
public sealed record NationalRegistrationNumber : IPartyIdentifier<NationalRegistrationNumber>
{
    /// <summary>ISO 20022 Max35Text.</summary>
    public const int MaxLength = 35;

    private NationalRegistrationNumber(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<NationalRegistrationNumber> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "national registration number").Map(value => new NationalRegistrationNumber(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
