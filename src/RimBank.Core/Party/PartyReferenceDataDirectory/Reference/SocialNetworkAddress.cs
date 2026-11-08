using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's social network profile: BIAN Reference <c>SocialNetworkContacts</c>, a <c>ContactPoint</c> read through its
/// ISO 20022 <c>ElectronicAddress.URLAddress</c> (roadmap 1.19, ADR 0031). It is trimmed, at most 2048 characters, and an
/// absolute <c>https</c> URL with a host. It is kept as written. Personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<SocialNetworkAddress>))]
public sealed record SocialNetworkAddress : IPartyReferenceText<SocialNetworkAddress>
{
    /// <summary>ISO 20022 Max2048Text.</summary>
    public const int MaxLength = 2048;

    private const string Message =
        "The social network contact must be an absolute https URL, for example https://www.linkedin.com/in/name.";

    private SocialNetworkAddress(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<SocialNetworkAddress> TryCreate(string? text) =>
        text?.Trim() switch
        {
            { Length: > MaxLength } =>
                PartyReferenceErrors.ValueInvalid($"The social network contact must be at most {MaxLength} characters."),
            { } value when IsHttpsUrl(value) => new SocialNetworkAddress(value),
            _ => PartyReferenceErrors.ValueInvalid(Message),
        };

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsHttpsUrl(string value) =>
        !value.Any(char.IsWhiteSpace)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.Length > 0;
}
