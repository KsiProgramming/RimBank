using System.Numerics;

namespace RimBank.SharedKernel;

public readonly partial record struct Money
{
    /// <summary>
    /// Divides the amount into <paramref name="parts"/> equal parts in whole minor units, for example
    /// 10.00 EUR into 3.34, 3.33 and 3.33. The parts always add up to exactly the original amount.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parts"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">The amount is not rounded to the currency's minor units.</exception>
    public IReadOnlyList<Money> Split(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parts, 1);

        var ratios = new int[parts];
        Array.Fill(ratios, 1);
        return Allocate(ratios);
    }

    /// <summary>
    /// Divides the amount in proportion to <paramref name="ratios"/>, in whole minor units, for example
    /// 100.00 EUR by 70, 20 and 10 into 70.00, 20.00 and 10.00. The parts always add up to exactly the original
    /// amount, keep its sign, and each differs from its exact share by less than one minor unit.
    /// </summary>
    /// <remarks>
    /// Each part first gets its exact share truncated towards zero. The minor units left over (fewer than the number
    /// of parts) go one each to the parts with the largest truncated remainder, and to the first of them on a tie
    /// (the largest-remainder method, ADR 0009). The calculation is exact, in whole minor units.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="ratios"/> is empty or has a ratio that is not positive.</exception>
    /// <exception cref="InvalidOperationException">The amount is not rounded to the currency's minor units.</exception>
    public IReadOnlyList<Money> Allocate(ReadOnlySpan<int> ratios)
    {
        if (ratios.IsEmpty)
        {
            throw new ArgumentException("At least one ratio is needed.", nameof(ratios));
        }

        BigInteger total = 0;
        foreach (var ratio in ratios)
        {
            if (ratio <= 0)
            {
                throw new ArgumentException("Every ratio must be positive.", nameof(ratios));
            }

            total += ratio;
        }

        if (!IsRoundedToMinorUnits)
        {
            throw new InvalidOperationException(
                $"Only an amount rounded to {Currency.MinorUnits} decimals can be allocated; round it first.");
        }

        var unitsPerMajor = BigInteger.Pow(10, Currency.MinorUnits);
        var units = (BigInteger)(Amount * (decimal)unitsPerMajor);
        var sign = units.Sign;

        var shares = new BigInteger[ratios.Length];
        var remainders = new BigInteger[ratios.Length];
        var allocated = BigInteger.Zero;
        for (var i = 0; i < ratios.Length; i++)
        {
            // DivRem truncates towards zero, so a negative amount gets negative shares and remainders.
            shares[i] = BigInteger.DivRem(units * ratios[i], total, out remainders[i]);
            allocated += shares[i];
        }

        var leftOver = (int)BigInteger.Abs(units - allocated);
        var order = Enumerable.Range(0, ratios.Length)
            .OrderByDescending(i => BigInteger.Abs(remainders[i]))
            .ThenBy(i => i)
            .Take(leftOver);
        foreach (var i in order)
        {
            shares[i] += sign;
        }

        var currency = Currency;
        return shares
            .Select(share => new Money((decimal)share / (decimal)unitsPerMajor, currency))
            .ToArray()
            .AsReadOnly();
    }
}
