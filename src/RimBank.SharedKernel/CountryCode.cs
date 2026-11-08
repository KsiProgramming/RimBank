using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// An ISO 3166-1 country: its two-letter alpha-2 <see cref="Code"/> and its <see cref="NumericCode"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only the countries in RimBank's embedded ISO 3166-1 table exist (ADR 0030): the officially assigned alpha-2 codes.
/// A country is obtained with <see cref="TryCreate"/> for untrusted input (HTTP, MCP) or <see cref="From"/> for trusted
/// input (event replay, tests). Like <see cref="Currency"/>, the code must be exact: callers that accept looser input
/// (for example lower case) normalise it first.
/// </para>
/// <para><c>default(CountryCode)</c> has no code and is not a country: <see cref="TryCreate"/> never returns it.</para>
/// <para>JSON: a plain string such as <c>"FR"</c>.</para>
/// </remarks>
[JsonConverter(typeof(CountryCodeJsonConverter))]
public readonly partial record struct CountryCode
{
    /// <summary>Error code for a value that is not a country in the table.</summary>
    public const string InvalidCountryCode = "shared.invalid-country";

    private const string InvalidCountryMessage =
        "The country must be an ISO 3166-1 alpha-2 code of two upper-case letters, for example FR.";

    private static readonly Error _invalidCountry = Error.Validation(InvalidCountryCode, InvalidCountryMessage);

    private static readonly FrozenDictionary<string, CountryCode> _byCode =
        Iso3166Table().ToFrozenDictionary(country => country.Code, StringComparer.Ordinal);

    private static readonly IReadOnlyList<CountryCode> _all =
        _byCode.Values.OrderBy(country => country.Code, StringComparer.Ordinal).ToArray().AsReadOnly();

    private CountryCode(string code, int numericCode)
    {
        Code = code;
        NumericCode = numericCode;
    }

    /// <summary>The ISO 3166-1 alpha-2 code, two upper-case letters (for example <c>FR</c>).</summary>
    public string Code { get; }

    /// <summary>The ISO 3166-1 numeric code (for example 250 for FR).</summary>
    public int NumericCode { get; }

    /// <summary>Every country in the table, ordered by code.</summary>
    public static IReadOnlyList<CountryCode> All => _all;

    /// <summary>The error returned for a value that is not a country in the table.</summary>
    public static Error InvalidCountry => _invalidCountry;

    /// <summary>True for <c>default(CountryCode)</c>, which is not a country.</summary>
    internal bool IsDefault => Code is null;

    /// <summary>
    /// Finds the country with this exact code (case-sensitive, no trimming).
    /// Fails with <c>shared.invalid-country</c> (Validation) for anything else.
    /// </summary>
    public static Result<CountryCode> TryCreate(string? code) =>
        code is not null && _byCode.TryGetValue(code, out var country)
            ? country
            : _invalidCountry;

    /// <summary>Finds the country with this exact code, for trusted input.</summary>
    /// <exception cref="ArgumentException">The code is not a country in the table (a programmer error).</exception>
    public static CountryCode From(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _byCode.TryGetValue(code, out var country)
            ? country
            : throw new ArgumentException($"'{code}' is not a country in the ISO 3166-1 table (ADR 0030).", nameof(code));
    }

    /// <summary>The alpha-2 code, for example <c>FR</c> (empty for <c>default(CountryCode)</c>).</summary>
    public override string ToString() => Code ?? string.Empty;
}
