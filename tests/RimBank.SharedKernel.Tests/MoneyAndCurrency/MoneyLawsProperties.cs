using FsCheck.Xunit;
using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.Tests;

// Algebraic laws of Money (ADR 0009). Each property runs 100 generated cases by default.
[Properties(Arbitrary = [typeof(MoneyArbitraries)])]
public sealed class MoneyLawsProperties
{
    [Property]
    public bool Addition_is_commutative(SameCurrencyPair pair) =>
        pair.First + pair.Second == pair.Second + pair.First;

    [Property]
    public bool Addition_is_associative(SameCurrencyTriple triple) =>
        (triple.First + triple.Second) + triple.Third == triple.First + (triple.Second + triple.Third);

    [Property]
    public bool Zero_is_the_identity(Money money) =>
        money + Money.Zero(money.Currency) == money && Money.Zero(money.Currency) + money == money;

    [Property]
    public bool Subtracting_an_amount_from_itself_gives_zero(Money money) =>
        (money - money).IsZero;

    [Property]
    public bool Adding_then_subtracting_gives_the_original(SameCurrencyPair pair) =>
        pair.First + pair.Second - pair.Second == pair.First;

    [Property]
    public bool Negating_twice_gives_the_original(Money money) => -(-money) == money;

    [Property]
    public bool Adding_the_negation_gives_zero(Money money) => (money + -money).IsZero;

    [Property]
    public bool Comparison_agrees_with_the_amounts(SameCurrencyPair pair) =>
        pair.First.CompareTo(pair.Second) == pair.First.Amount.CompareTo(pair.Second.Amount)
        && (pair.First < pair.Second) == (pair.First.Amount < pair.Second.Amount);

    [Property]
    public bool Multiplication_distributes_over_addition(SameCurrencyPair pair, short hundredths)
    {
        var factor = hundredths / 100m;
        return (pair.First + pair.Second) * factor == pair.First * factor + pair.Second * factor;
    }

    [Property]
    public bool Multiplying_by_one_gives_the_original(Money money) => money * 1m == money;

    [Property]
    public bool Different_currencies_never_combine(Money money, Currency other)
    {
        if (other == money.Currency)
        {
            return true;
        }

        var foreign = Money.Zero(other);
        return money.TryAdd(foreign).Error == Money.CurrencyMismatch
            && money.TrySubtract(foreign).Error == Money.CurrencyMismatch
            && Throws(() => _ = money + foreign)
            && Throws(() => _ = money - foreign)
            && Throws(() => _ = money.CompareTo(foreign));
    }

    [Property]
    public bool Rounding_is_idempotent(Money money) => money.Round().Round() == money.Round();

    [Property]
    public bool Rounding_moves_the_amount_by_at_most_half_a_minor_unit(Money money)
    {
        var halfMinorUnit = 0.5m / Pow10(money.Currency.MinorUnits);
        return Math.Abs((money.Round() - money).Amount) <= halfMinorUnit
            && Math.Abs((money.Round(MidpointRounding.AwayFromZero) - money).Amount) <= halfMinorUnit;
    }

    [Property]
    public bool A_rounded_amount_is_rounded_to_minor_units(Money money) =>
        money.Round().IsRoundedToMinorUnits && money.Round(MidpointRounding.AwayFromZero).IsRoundedToMinorUnits;

    [Property]
    public bool Rounding_keeps_a_rounded_amount_unchanged(RoundedMoney rounded) =>
        rounded.Value.IsRoundedToMinorUnits && rounded.Value.Round() == rounded.Value;

    [Property]
    public bool Rounding_keeps_the_currency(Money money) => money.Round().Currency == money.Currency;

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static decimal Pow10(int exponent) => exponent switch
    {
        0 => 1m,
        2 => 100m,
        3 => 1000m,
        _ => throw new ArgumentOutOfRangeException(nameof(exponent)),
    };
}
