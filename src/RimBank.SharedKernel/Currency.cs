using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace RimBank.SharedKernel;

/// <summary>
/// An ISO 4217 currency: its three-letter <see cref="Code"/>, its <see cref="NumericCode"/> and the number of
/// <see cref="MinorUnits"/> (decimal places) its amounts are settled in.
/// </summary>
/// <remarks>
/// <para>
/// Only the currencies in RimBank's embedded ISO 4217 table exist (ADR 0009): every code of ISO 4217 List One
/// except fund codes and codes without minor units. A currency is obtained with <see cref="TryCreate"/> for
/// untrusted input (HTTP, MCP) or <see cref="From"/> for trusted input (event replay, tests).
/// </para>
/// <para>
/// <c>default(Currency)</c> cannot be prevented for a struct. It has no code and is not a currency:
/// <see cref="Money"/> refuses it, and <see cref="TryCreate"/> never returns it.
/// </para>
/// <para>JSON: a plain string such as <c>"EUR"</c>.</para>
/// </remarks>
[JsonConverter(typeof(CurrencyJsonConverter))]
public readonly partial record struct Currency
{
    /// <summary>Error code for a value that is not a currency in the table.</summary>
    public const string InvalidCurrencyCode = "shared.invalid-currency";

    private const string InvalidCurrencyMessage =
        "The currency must be an ISO 4217 code of three upper-case letters, for example EUR.";

    private static readonly Error _invalidCurrency = Error.Validation(InvalidCurrencyCode, InvalidCurrencyMessage);

    private static readonly FrozenDictionary<string, Currency> _byCode =
        Iso4217Table().ToFrozenDictionary(currency => currency.Code, StringComparer.Ordinal);

    private static readonly IReadOnlyList<Currency> _all =
        _byCode.Values.OrderBy(currency => currency.Code, StringComparer.Ordinal).ToArray().AsReadOnly();

    private Currency(string code, int numericCode, int minorUnits)
    {
        Code = code;
        NumericCode = numericCode;
        MinorUnits = minorUnits;
    }

    /// <summary>The ISO 4217 alphabetic code, three upper-case letters (for example <c>EUR</c>).</summary>
    public string Code { get; }

    /// <summary>The ISO 4217 numeric code (for example 978 for EUR).</summary>
    public int NumericCode { get; }

    /// <summary>The number of decimal places of the currency's minor unit: 0, 2 or 3.</summary>
    public int MinorUnits { get; }

    /// <summary>Every currency in the table, ordered by code.</summary>
    public static IReadOnlyList<Currency> All => _all;

    /// <summary>The error returned for a value that is not a currency in the table.</summary>
    public static Error InvalidCurrency => _invalidCurrency;

    /// <summary>True for <c>default(Currency)</c>, which is not a currency.</summary>
    internal bool IsDefault => Code is null;

    /// <summary>
    /// Finds the currency with this exact code (case-sensitive, no trimming).
    /// Fails with <c>shared.invalid-currency</c> (Validation) for anything else.
    /// </summary>
    public static Result<Currency> TryCreate(string? code) =>
        code is not null && _byCode.TryGetValue(code, out var currency)
            ? currency
            : _invalidCurrency;

    /// <summary>Finds the currency with this exact code, for trusted input.</summary>
    /// <exception cref="ArgumentException">The code is not a currency in the table (a programmer error).</exception>
    public static Currency From(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _byCode.TryGetValue(code, out var currency)
            ? currency
            : throw new ArgumentException($"'{code}' is not a currency in the ISO 4217 table (ADR 0009).", nameof(code));
    }

    /// <summary>The alphabetic code, for example <c>EUR</c> (empty for <c>default(Currency)</c>).</summary>
    public override string ToString() => Code ?? string.Empty;
}
