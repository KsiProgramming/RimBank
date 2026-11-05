namespace RimBank.SharedKernel.Tests.MoneyAndCurrency;

public sealed class MoneyAllocationTests
{
    private static Money Of(decimal amount, string code = "EUR") => Money.Of(amount, Currency.From(code));

    [Fact]
    public void Split_gives_the_extra_minor_unit_to_the_first_part() =>
        Of(10.00m).Split(3).ShouldBe([Of(3.34m), Of(3.33m), Of(3.33m)]);

    [Fact]
    public void Split_into_one_part_gives_the_amount() =>
        Of(10.01m).Split(1).ShouldBe([Of(10.01m)]);

    [Fact]
    public void Split_of_a_small_amount_gives_zero_parts() =>
        Of(0.02m).Split(3).ShouldBe([Of(0.01m), Of(0.01m), Of(0m)]);

    [Fact]
    public void Allocate_follows_the_ratios() =>
        Of(100.00m).Allocate([70, 20, 10]).ShouldBe([Of(70m), Of(20m), Of(10m)]);

    [Fact]
    public void Allocate_gives_left_over_units_to_the_largest_remainders()
    {
        // Exact shares: 0.0333…, 0.0666… → truncated 0.03 and 0.06, one cent left for the larger remainder.
        Of(0.10m).Allocate([1, 2]).ShouldBe([Of(0.03m), Of(0.07m)]);

        // Exact shares 5.00 and 0.0499…: the cent never goes to the exact share.
        Of(5.05m).Allocate([100, 1]).ShouldBe([Of(5.00m), Of(0.05m)]);
    }

    [Fact]
    public void Allocate_uses_the_minor_units_of_the_currency()
    {
        Of(100m, "JPY").Split(3).ShouldBe([Of(34m, "JPY"), Of(33m, "JPY"), Of(33m, "JPY")]);
        Of(1.000m, "BHD").Split(3).ShouldBe([Of(0.334m, "BHD"), Of(0.333m, "BHD"), Of(0.333m, "BHD")]);
    }

    [Fact]
    public void Allocate_keeps_the_sign_of_a_negative_amount() =>
        Of(-10.00m).Split(3).ShouldBe([Of(-3.34m), Of(-3.33m), Of(-3.33m)]);

    [Fact]
    public void Allocate_of_zero_gives_zero_parts() =>
        Money.Zero(Currency.From("EUR")).Split(2).ShouldAllBe(part => part.IsZero);

    [Fact]
    public void Allocate_refuses_an_unrounded_amount() =>
        Should.Throw<InvalidOperationException>(() => Of(10.005m).Split(2)).Message.ShouldContain("round it first");

    [Fact]
    public void Allocate_refuses_empty_or_non_positive_ratios()
    {
        Should.Throw<ArgumentException>(() => Of(1m).Allocate([]));
        Should.Throw<ArgumentException>(() => Of(1m).Allocate([1, 0]));
        Should.Throw<ArgumentException>(() => Of(1m).Allocate([2, -1]));
    }

    [Fact]
    public void Split_refuses_fewer_than_one_part() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Of(1m).Split(0));

    [Fact]
    public void Allocate_refuses_the_default_money() =>
        Should.Throw<InvalidOperationException>(() => default(Money).Split(2));
}
