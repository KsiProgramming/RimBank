using FsCheck;
using FsCheck.Xunit;
using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.MoneyAndCurrency.Tests;

// No minor unit is created or lost when money is divided (mission, quality bar; ADR 0009).
[Properties(Arbitrary = [typeof(MoneyArbitraries)])]
public sealed class MoneyAllocationProperties
{
    [Property]
    public bool The_parts_add_up_to_exactly_the_original(RoundedMoney rounded, NonEmptyArray<PositiveInt> ratios)
    {
        var parts = rounded.Value.Allocate(Ratios(ratios));
        return parts.Aggregate(Money.Zero(rounded.Value.Currency), (sum, part) => sum + part) == rounded.Value;
    }

    [Property]
    public bool There_is_one_part_per_ratio_in_the_same_currency_and_rounded(RoundedMoney rounded, NonEmptyArray<PositiveInt> ratios)
    {
        var parts = rounded.Value.Allocate(Ratios(ratios));
        return parts.Count == ratios.Get.Length
            && parts.All(part => part.Currency == rounded.Value.Currency && part.IsRoundedToMinorUnits);
    }

    [Property]
    public bool Each_part_is_within_one_minor_unit_of_its_exact_share(RoundedMoney rounded, NonEmptyArray<PositiveInt> ratios)
    {
        var weights = Ratios(ratios);
        var total = weights.Sum(ratio => (decimal)ratio);
        var minorUnit = 1m / Pow10(rounded.Value.Currency.MinorUnits);
        var parts = rounded.Value.Allocate(weights);

        return parts.Select((part, i) => Math.Abs(part.Amount - (rounded.Value.Amount * weights[i] / total)))
            .All(difference => difference < minorUnit);
    }

    [Property]
    public bool Parts_keep_the_sign_of_the_amount(RoundedMoney rounded, NonEmptyArray<PositiveInt> ratios) =>
        rounded.Value.Allocate(Ratios(ratios)).All(part => part.IsZero || part.IsNegative == rounded.Value.IsNegative);

    [Property]
    public bool Split_parts_differ_by_at_most_one_minor_unit(RoundedMoney rounded, PositiveInt parts)
    {
        var count = (parts.Get % 50) + 1;
        var split = rounded.Value.Split(count);
        var minorUnit = 1m / Pow10(rounded.Value.Currency.MinorUnits);

        return split.Max(part => part.Amount) - split.Min(part => part.Amount) <= minorUnit;
    }

    private static int[] Ratios(NonEmptyArray<PositiveInt> ratios) =>
        ratios.Get.Select(ratio => ratio.Get).ToArray();

    private static decimal Pow10(int exponent) => exponent switch
    {
        0 => 1m,
        2 => 100m,
        3 => 1000m,
        _ => throw new ArgumentOutOfRangeException(nameof(exponent)),
    };
}
