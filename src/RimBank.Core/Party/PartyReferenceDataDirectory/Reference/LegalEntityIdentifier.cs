using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's Legal Entity Identifier (ISO 17442), carried in BIAN <c>PartyLegalEntityReference.Identification</c>
/// (ADR 0029): 18 upper-case letters or digits followed by two check digits, which pass the ISO 7064 MOD 97-10 check.
/// It is trimmed and upper-cased.
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<LegalEntityIdentifier>))]
public sealed record LegalEntityIdentifier : IPartyIdentifier<LegalEntityIdentifier>
{
    /// <summary>An LEI is always 20 characters.</summary>
    public const int Length = 20;

    private const string FormatMessage =
        "The LEI must be 20 characters: 18 letters or digits followed by 2 check digits (ISO 17442).";

    private LegalEntityIdentifier(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<LegalEntityIdentifier> TryCreate(string? text)
    {
        var value = text?.Trim().ToUpperInvariant();
        return value switch
        {
            null or "" => PartyReferenceErrors.ValueInvalid("The LEI must not be empty."),
            _ when !HasFormat(value) => PartyReferenceErrors.ValueInvalid(FormatMessage),
            _ when !HasValidCheckDigits(value) => PartyReferenceErrors.ValueInvalid("The LEI check digits are not valid (ISO 7064 MOD 97-10)."),
            _ => new LegalEntityIdentifier(value),
        };
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool HasFormat(string value) =>
        value.Length == Length
        && value[..18].All(char.IsAsciiLetterOrDigit)
        && value[18..].All(char.IsAsciiDigit);

    // ISO 7064 MOD 97-10: each letter becomes its two-digit number (A = 10 … Z = 35), and the whole number modulo 97 is 1.
    private static bool HasValidCheckDigits(string value) =>
        value.Aggregate(0, (remainder, character) => char.IsAsciiDigit(character)
            ? ((remainder * 10) + (character - '0')) % 97
            : ((remainder * 100) + (character - 'A' + 10)) % 97) == 1;
}
