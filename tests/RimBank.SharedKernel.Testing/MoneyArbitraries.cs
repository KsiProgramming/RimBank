using FsCheck;
using FsCheck.Fluent;

namespace RimBank.SharedKernel.Testing;

/// <summary>
/// FsCheck generators for <see cref="Currency"/> and <c>Money</c>, shared by every context's property tests
/// (for example 3.3 "balance = sum of transactions" and 6.2 "debits = credits").
/// </summary>
/// <remarks>
/// Register with <c>[Property(Arbitrary = [typeof(MoneyArbitraries)])]</c>, or use the <c>…Gen</c> members directly
/// to build context-specific generators.
/// </remarks>
public static class MoneyArbitraries
{
    // Every minor-unit size (0, 2 and 3) is drawn equally often, so the rarer 0- and 3-decimal currencies
    // are exercised as much as the common 2-decimal ones.
    private static readonly Gen<Currency> _currency = Gen.OneOf(
        Currency.All.GroupBy(currency => currency.MinorUnits).Select(group => Gen.Elements(group.ToArray())));

    /// <summary>Any currency in the table, with 0, 2 and 3 minor units equally likely.</summary>
    public static Gen<Currency> CurrencyGen => _currency;

    /// <summary>The arbitrary FsCheck uses for <see cref="Currency"/> parameters.</summary>
    public static Arbitrary<Currency> CurrencyArbitrary() => Arb.From(_currency);
}
