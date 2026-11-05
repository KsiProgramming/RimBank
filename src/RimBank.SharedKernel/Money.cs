using System.Numerics;

namespace RimBank.SharedKernel;

/// <summary>
/// An amount of money in one <see cref="SharedKernel.Currency"/>. The amount is a <see cref="decimal"/>; <c>double</c>
/// and <c>float</c> are never used for money (mission, quality bar).
/// </summary>
/// <remarks>
/// <para>
/// The amount may have any sign and any scale, so balances can be negative and interest can be calculated
/// unrounded. Rounding to the currency's minor units is always explicit: <see cref="Round()"/> uses
/// <see cref="MidpointRounding.ToEven"/> (banker's rounding), and <see cref="Round(MidpointRounding)"/> names
/// another mode. Money is divided with <c>Allocate</c> or <c>Split</c>, never with a division operator, so no
/// minor unit is created or lost (ADR 0009).
/// </para>
/// <para>
/// Combining two amounts in different currencies with an operator is a programmer error and throws
/// <see cref="InvalidOperationException"/>. At an input boundary, use <see cref="TryAdd"/> and
/// <see cref="TrySubtract"/>, which return <c>shared.currency-mismatch</c> (Validation) instead.
/// </para>
/// <para>
/// <c>default(Money)</c> cannot be prevented for a struct. Its currency is <c>default(Currency)</c>, and every
/// operation on it throws <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public readonly record struct Money :
    IComparable<Money>,
    IComparisonOperators<Money, Money, bool>,
    IAdditionOperators<Money, Money, Money>,
    ISubtractionOperators<Money, Money, Money>,
    IUnaryNegationOperators<Money, Money>,
    IMultiplyOperators<Money, decimal, Money>
{
    /// <summary>Error code for combining amounts in different currencies at an input boundary.</summary>
    public const string CurrencyMismatchCode = "shared.currency-mismatch";

    private static readonly Error _currencyMismatch =
        Error.Validation(CurrencyMismatchCode, "The amounts must be in the same currency.");

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount, in units of the currency (for example 10.50 for ten euros fifty).</summary>
    public decimal Amount { get; }

    /// <summary>The currency of the amount.</summary>
    public Currency Currency { get; }

    /// <summary>True when the amount has no more decimal places than the currency's minor units.</summary>
    public bool IsRoundedToMinorUnits => decimal.Round(Valid().Amount, Currency.MinorUnits) == Amount;

    /// <summary>True when the amount is zero (in any scale, for example 0 or 0.00).</summary>
    public bool IsZero => Valid().Amount == 0m;

    /// <summary>True when the amount is below zero. Zero is not negative.</summary>
    public bool IsNegative => Valid().Amount < 0m;

    /// <summary>The error returned by <see cref="TryAdd"/> and <see cref="TrySubtract"/> for different currencies.</summary>
    public static Error CurrencyMismatch => _currencyMismatch;

    /// <summary>The amount <paramref name="amount"/> in <paramref name="currency"/>, unrounded.</summary>
    /// <exception cref="ArgumentException"><paramref name="currency"/> is <c>default(Currency)</c>.</exception>
    public static Money Of(decimal amount, Currency currency) =>
        currency.IsDefault
            ? throw new ArgumentException("default(Currency) is not a currency.", nameof(currency))
            : new Money(amount, currency);

    /// <summary>Zero in <paramref name="currency"/>.</summary>
    public static Money Zero(Currency currency) => Of(0m, currency);

    /// <summary>The sum of two amounts in the same currency.</summary>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static Money operator +(Money left, Money right) => left.Add(right);

    /// <summary>The difference of two amounts in the same currency.</summary>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static Money operator -(Money left, Money right) => left.Subtract(right);

    /// <summary>The same amount with the opposite sign.</summary>
    public static Money operator -(Money value) => value.Negate();

    /// <summary>The amount multiplied by <paramref name="factor"/>, unrounded.</summary>
    public static Money operator *(Money left, decimal factor) => left.Multiply(factor);

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan" />
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual" />
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan" />
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual" />
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>The sum with <paramref name="other"/>, which must be in the same currency.</summary>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public Money Add(Money other) => new(Amount + SameCurrency(other).Amount, Currency);

    /// <summary>The difference with <paramref name="other"/>, which must be in the same currency.</summary>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public Money Subtract(Money other) => new(Amount - SameCurrency(other).Amount, Currency);

    /// <summary>The same amount with the opposite sign.</summary>
    public Money Negate() => new(-Valid().Amount, Currency);

    /// <summary>The amount multiplied by <paramref name="factor"/>, unrounded: round it explicitly afterwards.</summary>
    public Money Multiply(decimal factor) => new(Valid().Amount * factor, Currency);

    /// <summary>The sum, or <c>shared.currency-mismatch</c> when the currencies differ.</summary>
    public Result<Money> TryAdd(Money other) =>
        Valid().Currency == other.Valid().Currency ? Add(other) : _currencyMismatch;

    /// <summary>The difference, or <c>shared.currency-mismatch</c> when the currencies differ.</summary>
    public Result<Money> TrySubtract(Money other) =>
        Valid().Currency == other.Valid().Currency ? Subtract(other) : _currencyMismatch;

    /// <summary>Rounds to the currency's minor units with banker's rounding (<see cref="MidpointRounding.ToEven"/>).</summary>
    public Money Round() => Round(MidpointRounding.ToEven);

    /// <summary>Rounds to the currency's minor units with the named <paramref name="mode"/>.</summary>
    public Money Round(MidpointRounding mode) => new(decimal.Round(Valid().Amount, Currency.MinorUnits, mode), Currency);

    /// <summary>Compares two amounts in the same currency.</summary>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public int CompareTo(Money other) => Amount.CompareTo(SameCurrency(other).Amount);

    private Money Valid() =>
        Currency.IsDefault ? throw new InvalidOperationException("default(Money) has no currency and is not money.") : this;

    private Money SameCurrency(Money other) =>
        Valid().Currency == other.Valid().Currency
            ? other
            : throw new InvalidOperationException(
                $"Cannot combine {Currency.Code} and {other.Currency.Code}: amounts must be in the same currency.");
}
