using FsCheck;
using FsCheck.Fluent;

namespace RimBank.SharedKernel.Testing;

/// <summary>Two amounts in the same currency.</summary>
public sealed record SameCurrencyPair(Money First, Money Second);

/// <summary>Three amounts in the same currency.</summary>
public sealed record SameCurrencyTriple(Money First, Money Second, Money Third);

/// <summary>An amount already rounded to its currency's minor units.</summary>
public sealed record RoundedMoney(Money Value);

/// <summary>
/// FsCheck generators for <see cref="Currency"/> and <see cref="Money"/>, shared by every context's property tests
/// (for example 3.3 "balance = sum of transactions" and 6.2 "debits = credits").
/// </summary>
/// <remarks>
/// <para>
/// Register with <c>[Property(Arbitrary = [typeof(MoneyArbitraries)])]</c> (or <c>[Properties]</c> on the class).
/// A <see cref="Money"/> parameter then gets any amount (rounded or with up to four extra decimals); use
/// <see cref="RoundedMoney"/>, <see cref="SameCurrencyPair"/> or <see cref="SameCurrencyTriple"/> when a property
/// needs them. The <c>…Gen</c> members build context-specific generators.
/// </para>
/// <para>
/// Amounts stay below 10^12 units, far from <see cref="decimal"/>'s limits, so the arithmetic laws hold exactly.
/// Shrinking moves towards zero and towards fewer decimals.
/// </para>
/// </remarks>
public static class MoneyArbitraries
{
    private const int ExtraDecimals = 4;

    // Every minor-unit size (0, 2 and 3) is drawn equally often, so the rarer 0- and 3-decimal currencies
    // are exercised as much as the common 2-decimal ones.
    private static readonly Gen<Currency> _currency = Gen.OneOf(
        Currency.All.GroupBy(currency => currency.MinorUnits).Select(group => Gen.Elements(group.ToArray())));

    /// <summary>Any currency in the table, with 0, 2 and 3 minor units equally likely.</summary>
    public static Gen<Currency> CurrencyGen => _currency;

    /// <summary>Any amount in <paramref name="currency"/>, rounded to its minor units.</summary>
    public static Gen<Money> RoundedMoneyGen(Currency currency) =>
        AmountGen(currency.MinorUnits).Select(amount => Money.Of(amount, currency));

    /// <summary>Any amount in <paramref name="currency"/>: rounded, or with up to four decimals more than its minor units.</summary>
    public static Gen<Money> MoneyGen(Currency currency) =>
        from extra in Gen.Choose(0, ExtraDecimals)
        from amount in AmountGen(currency.MinorUnits + extra)
        select Money.Of(amount, currency);

    /// <summary>The arbitrary FsCheck uses for <see cref="Currency"/> parameters.</summary>
    public static Arbitrary<Currency> CurrencyArbitrary() => Arb.From(_currency);

    /// <summary>The arbitrary FsCheck uses for <see cref="Money"/> parameters: any currency, rounded or not.</summary>
    public static Arbitrary<Money> MoneyArbitrary() => Arb.From(_currency.SelectMany(MoneyGen), Shrink);

    /// <summary>The arbitrary for <see cref="RoundedMoney"/> parameters.</summary>
    public static Arbitrary<RoundedMoney> RoundedMoneyArbitrary() => Arb.From(
        _currency.SelectMany(RoundedMoneyGen).Select(money => new RoundedMoney(money)),
        rounded => Shrink(rounded.Value).Where(money => money.IsRoundedToMinorUnits).Select(money => new RoundedMoney(money)));

    /// <summary>The arbitrary for <see cref="SameCurrencyPair"/> parameters.</summary>
    public static Arbitrary<SameCurrencyPair> SameCurrencyPairArbitrary() => Arb.From(
        from currency in _currency
        from first in MoneyGen(currency)
        from second in MoneyGen(currency)
        select new SameCurrencyPair(first, second),
        pair => Shrink(pair.First).Select(first => pair with { First = first })
            .Concat(Shrink(pair.Second).Select(second => pair with { Second = second })));

    /// <summary>The arbitrary for <see cref="SameCurrencyTriple"/> parameters.</summary>
    public static Arbitrary<SameCurrencyTriple> SameCurrencyTripleArbitrary() => Arb.From(
        from currency in _currency
        from first in MoneyGen(currency)
        from second in MoneyGen(currency)
        from third in MoneyGen(currency)
        select new SameCurrencyTriple(first, second, third),
        triple => Shrink(triple.First).Select(first => triple with { First = first })
            .Concat(Shrink(triple.Second).Select(second => triple with { Second = second }))
            .Concat(Shrink(triple.Third).Select(third => triple with { Third = third })));

    // An amount with `decimals` decimal places and up to about 10^12 units; half of them are small
    // (below 10^6 units) so amounts near zero are common. Dividing an integer by a power of ten is exact.
    private static Gen<decimal> AmountGen(int decimals) =>
        from large in Gen.Elements(false, true)
        from high in Gen.Choose(-1_000_000, 1_000_000)
        from low in Gen.Choose(0, 999_999)
        let sign = high < 0 ? -1L : 1L
        let units = (large ? high * 1_000_000L : 0L) + (sign * low)
        select decimal.Divide(units, Pow10(decimals));

    // Towards zero, then towards the rounded amount, then towards half the amount (in whole minor units).
    private static IEnumerable<Money> Shrink(Money money)
    {
        if (money.IsZero)
        {
            yield break;
        }

        yield return Money.Zero(money.Currency);

        if (!money.IsRoundedToMinorUnits)
        {
            yield return money.Round(MidpointRounding.ToZero);
        }

        var minorUnits = Pow10(money.Currency.MinorUnits);
        var half = Money.Of(decimal.Truncate(money.Amount / 2m * minorUnits) / minorUnits, money.Currency);
        if (!half.IsZero && half != money)
        {
            yield return half;
        }
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
