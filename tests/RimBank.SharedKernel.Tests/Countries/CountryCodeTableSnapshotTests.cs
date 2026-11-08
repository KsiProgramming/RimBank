using System.Globalization;

namespace RimBank.SharedKernel.Tests;

// The snapshot (Iso3166.snapshot.csv) is the reviewed copy of the table: the 249 officially assigned ISO 3166-1
// alpha-2 codes with their numeric codes (ADR 0030). Any change to CountryCode.Iso3166.cs must change the snapshot
// in the same PR.
public sealed class CountryCodeTableSnapshotTests
{
    [Fact]
    public void The_table_matches_the_reviewed_snapshot()
    {
        var snapshot = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Countries", "Iso3166.snapshot.csv"))
            .Where(line => line.Length > 0);

        var table = CountryCode.All.Select(country => string.Create(
            CultureInfo.InvariantCulture,
            $"{country.Code},{country.NumericCode:D3}"));

        table.ShouldBe(snapshot);
    }

    [Fact]
    public void The_table_has_249_countries_ordered_by_code()
    {
        CountryCode.All.Count.ShouldBe(249);
        CountryCode.All.Select(country => country.Code).ShouldBe(
            CountryCode.All.Select(country => country.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_code_is_two_upper_case_letters_and_unique()
    {
        CountryCode.All.ShouldAllBe(country => country.Code.Length == 2 && country.Code.All(char.IsAsciiLetterUpper));
        CountryCode.All.Select(country => country.Code).Distinct().Count().ShouldBe(CountryCode.All.Count);
        CountryCode.All.Select(country => country.NumericCode).Distinct().Count().ShouldBe(CountryCode.All.Count);
    }

    [Fact]
    public void User_assigned_codes_are_left_out() =>
        CountryCode.All.Select(country => country.Code).ShouldNotContain(code =>
            code == "AA" || code == "ZZ" || (code[0] == 'Q' && code[1] >= 'M') || code[0] == 'X');
}
