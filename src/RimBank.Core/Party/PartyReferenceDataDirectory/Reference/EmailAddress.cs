using System.Buffers;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's email address: BIAN Reference <c>eMailAddress</c>, a <c>ContactPoint</c> read through its ISO 20022
/// <c>ElectronicAddress.EmailAddress</c> (roadmap 1.19, ADR 0031). It is trimmed, at most 256 characters, and has the
/// shape <c>local@domain.tld</c>: exactly one <c>@</c>, a local part, a domain of at least two non-empty labels, and no
/// whitespace or display name. It is kept as written: only a verification could prove it is delivered. Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<EmailAddress>))]
public sealed record EmailAddress : IPartyReferenceText<EmailAddress>
{
    /// <summary>ISO 20022 Max256Text.</summary>
    public const int MaxLength = 256;

    private const string Message = "The email address must look like name@example.com, with no spaces or display name.";

    private static readonly SearchValues<char> _forbidden = SearchValues.Create("<>()[],;:\"\\");

    private EmailAddress(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<EmailAddress> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "email address")
            .Bind<EmailAddress>(value => IsWellFormed(value) ? new EmailAddress(value) : PartyReferenceErrors.ValueInvalid(Message));

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsWellFormed(string value)
    {
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) || value.IndexOfAny(_forbidden) >= 0)
        {
            return false;
        }

        var parts = value.Split('@');
        return parts is [{ Length: > 0 }, var domain]
            && domain.Split('.') is { Length: >= 2 } labels
            && labels.All(label => label.Length > 0);
    }
}
