using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RimBank.SharedKernel;

public readonly partial record struct Money
{
    /// <summary>Error code for an amount that is not a valid amount in its currency.</summary>
    public const string InvalidAmountCode = "shared.invalid-amount";

    /// <summary>The field name used in the error for an invalid amount.</summary>
    public const string AmountField = "amount";

    /// <summary>The field name used in the error for an invalid currency.</summary>
    public const string CurrencyField = "currency";

    // decimal keeps at most 28 decimals; longer text would be rounded silently by decimal.Parse.
    private const int MaxDecimals = 28;

    private const string InvalidAmountMessage =
        "The amount must be a number with a dot as decimal separator and no more decimals than the currency allows, for example 10.50.";

    /// <summary>
    /// Creates money from untrusted text (HTTP, MCP): an amount such as <c>"10.50"</c> or <c>"-3"</c>, and an
    /// ISO 4217 code such as <c>"EUR"</c>.
    /// </summary>
    /// <remarks>
    /// The amount uses the invariant format only: an optional leading <c>-</c>, digits, and optionally a dot
    /// followed by at most as many digits as the currency's minor units (so <c>"10.500"</c> is refused for EUR).
    /// No whitespace, grouping separators, exponent or <c>+</c> sign. An invalid currency fails with
    /// <c>shared.invalid-currency</c> (field <c>currency</c>), an invalid amount with <c>shared.invalid-amount</c>
    /// (field <c>amount</c>), both of kind Validation.
    /// </remarks>
    public static Result<Money> TryCreate(string? amount, string? currencyCode)
    {
        var currency = Currency.TryCreate(currencyCode);
        if (currency.IsFailure)
        {
            return FieldError(Currency.InvalidCurrencyCode, currency.Error.Message, CurrencyField);
        }

        return TryParseAmount(amount, currency.Value.MinorUnits, out var value)
            ? new Money(value, currency.Value)
            : FieldError(InvalidAmountCode, InvalidAmountMessage, AmountField);
    }

    /// <summary>
    /// The amount in the invariant format, with at least the currency's minor units, for example <c>"10.50"</c>,
    /// <c>"1000"</c> for JPY, or <c>"0.12345"</c> for an unrounded EUR amount.
    /// </summary>
    public string FormatAmount()
    {
        var amount = Valid().Amount == 0m ? 0m : Amount; // never "-0.00"
        var decimals = Math.Max(amount.Scale, Currency.MinorUnits);
        return amount.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>The amount and currency, for example <c>"10.50 EUR"</c>, whatever the current culture.</summary>
    public override string ToString() => Currency.IsDefault ? string.Empty : $"{FormatAmount()} {Currency.Code}";

    // Strict syntax, any scale up to decimal's limit. Used by TryCreate (with the currency's minor units)
    // and by the JSON converter (with MaxDecimals, because stored values may be unrounded).
    internal static bool TryParseAmount(string? text, int maxDecimals, out decimal amount)
    {
        amount = 0m;
        if (text is null || text.Length > 64)
        {
            return false;
        }

        var match = AmountPattern().Match(text);
        return match.Success
            && match.Groups["decimals"].Length <= Math.Min(maxDecimals, MaxDecimals)
            && decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private static Error FieldError(string code, string message, string field) =>
        Error.Validation(code, message, new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>> { [field] = [message] }));

    [GeneratedRegex("^-?[0-9]+(?:\\.(?<decimals>[0-9]+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();
}
