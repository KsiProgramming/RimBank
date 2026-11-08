using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The salutation the party prefers, for example <c>Ms</c> or <c>Dr</c>: BIAN Reference <c>PartyNameSalutation</c>
/// (roadmap 1.18, ADR 0029). It is free text, trimmed, not blank and at most 35 characters (ISO 20022 Max35Text).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<PartyNameSalutation>))]
public sealed record PartyNameSalutation : IPartyReferenceText<PartyNameSalutation>
{
    /// <summary>ISO 20022 Max35Text.</summary>
    public const int MaxLength = 35;

    private PartyNameSalutation(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<PartyNameSalutation> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "salutation").Map(value => new PartyNameSalutation(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
