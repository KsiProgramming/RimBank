using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's mobile phone number: BIAN Reference <c>CellPhoneNumber</c>, a <c>ContactPoint</c> read through its ISO 20022
/// <c>PhoneAddress.MobileNumber</c> (roadmap 1.19, ADR 0031). Spaces, hyphens and dots are removed, and the rest must be
/// an E.164 number: <c>+</c>, a country code that does not start with 0, and 7 to 15 digits in all. It is stored
/// normalised, for example <c>+33612345678</c>, so ISO 20022's <c>+33-612345678</c> reads the same. Brackets are refused,
/// because removing them from <c>+33 (0)6…</c> would keep the trunk 0 and give a wrong number. Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<MobileNumber>))]
public sealed record MobileNumber : IPartyReferenceText<MobileNumber>
{
    /// <summary>The fewest digits accepted: the shortest numbers in use (for example Niue, +683 xxxx).</summary>
    public const int MinDigits = 7;

    /// <summary>The most digits E.164 allows.</summary>
    public const int MaxDigits = 15;

    private const string Message = "The mobile number must be an international E.164 number, for example +33612345678.";

    private MobileNumber(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<MobileNumber> TryCreate(string? text)
    {
        var normalised = string.Concat((text ?? string.Empty).Where(c => c is not (' ' or '-' or '.')));
        return normalised is ['+', >= '1' and <= '9', .. var rest]
            && rest.All(char.IsAsciiDigit)
            && rest.Length + 1 is >= MinDigits and <= MaxDigits
            ? new MobileNumber(normalised)
            : PartyReferenceErrors.ValueInvalid(Message);
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
