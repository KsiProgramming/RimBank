using System.Globalization;

namespace RimBank.SharedKernel.Tests;

// The snapshot (Iso4217.snapshot.csv) is the reviewed copy of the table: SIX ISO 4217 List One,
// edition 2026-09-17, without fund codes and codes with "N.A." minor units (ADR 0009).
// Any change to Currency.Iso4217.cs must change the snapshot in the same PR.
public sealed class CurrencyTableSnapshotTests
{
    private static readonly string[] _fundCodes = ["BOV", "CHE", "CHW", "CLF", "COU", "MXV", "USN", "UYI", "UYW", "XAD"];

    private static readonly string[] _codesWithoutMinorUnits =
        ["XAG", "XAU", "XBA", "XBB", "XBC", "XBD", "XDR", "XPD", "XPT", "XSU", "XTS", "XUA", "XXX"];

    [Fact]
    public void The_table_matches_the_reviewed_snapshot()
    {
        var snapshot = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "MoneyAndCurrency", "Iso4217.snapshot.csv"))
            .Where(line => line.Length > 0);

        var table = Currency.All.Select(currency => string.Create(
            CultureInfo.InvariantCulture,
            $"{currency.Code},{currency.NumericCode:D3},{currency.MinorUnits}"));

        table.ShouldBe(snapshot);
    }

    [Fact]
    public void The_table_has_155_currencies_ordered_by_code()
    {
        Currency.All.Count.ShouldBe(155);
        Currency.All.Select(currency => currency.Code).ShouldBe(
            Currency.All.Select(currency => currency.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_code_is_three_upper_case_letters_and_unique()
    {
        Currency.All.ShouldAllBe(currency => currency.Code.Length == 3 && currency.Code.All(char.IsAsciiLetterUpper));
        Currency.All.Select(currency => currency.Code).Distinct().Count().ShouldBe(Currency.All.Count);
        Currency.All.Select(currency => currency.NumericCode).Distinct().Count().ShouldBe(Currency.All.Count);
    }

    [Fact]
    public void Minor_units_are_0_2_or_3() =>
        Currency.All.Select(currency => currency.MinorUnits).Distinct().Order().ShouldBe([0, 2, 3]);

    [Fact]
    public void Fund_codes_and_codes_without_minor_units_are_left_out() =>
        Currency.All.Select(currency => currency.Code).ShouldNotContain(code =>
            _fundCodes.Contains(code) || _codesWithoutMinorUnits.Contains(code));
}
