using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The full legal name of the party: BIAN Reference <c>PartyFullLegalName</c> (roadmap 1.18, ADR 0029). It is trimmed,
/// not blank and at most 140 characters (ISO 20022 Max140Text). Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<PartyFullLegalName>))]
public sealed record PartyFullLegalName : IPartyReferenceText<PartyFullLegalName>
{
    /// <summary>ISO 20022 Max140Text.</summary>
    public const int MaxLength = 140;

    private PartyFullLegalName(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<PartyFullLegalName> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "full legal name").Map(value => new PartyFullLegalName(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
