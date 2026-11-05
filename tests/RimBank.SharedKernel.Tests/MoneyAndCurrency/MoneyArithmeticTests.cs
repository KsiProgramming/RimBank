namespace RimBank.SharedKernel.Tests.MoneyAndCurrency;

public sealed class MoneyArithmeticTests
{
    private static readonly Currency _eur = Currency.From("EUR");
    private static readonly Currency _usd = Currency.From("USD");

    private static Money Eur(decimal amount) => Money.Of(amount, _eur);

    [Fact]
    public void Of_keeps_the_amount_and_currency_unrounded()
    {
        var money = Eur(10.12345m);

        money.Amount.ShouldBe(10.12345m);
        money.Currency.ShouldBe(_eur);
    }

    [Fact]
    public void Of_refuses_the_default_currency() =>
        Should.Throw<ArgumentException>(() => Money.Of(1m, default));

    [Fact]
    public void Zero_is_zero_in_the_currency()
    {
        Money.Zero(_eur).Amount.ShouldBe(0m);
        Money.Zero(_eur).IsZero.ShouldBeTrue();
        Money.Zero(_eur).IsNegative.ShouldBeFalse();
    }

    [Fact]
    public void Amounts_with_a_different_scale_are_equal()
    {
        Eur(10.5m).ShouldBe(Eur(10.50m));
        Eur(10.5m).GetHashCode().ShouldBe(Eur(10.50m).GetHashCode());
    }

    [Fact]
    public void The_same_amount_in_another_currency_is_not_equal() =>
        Eur(10m).ShouldNotBe(Money.Of(10m, _usd));

    [Fact]
    public void Addition_and_subtraction_are_exact()
    {
        (Eur(0.1m) + Eur(0.2m)).ShouldBe(Eur(0.3m));
        (Eur(10.00m) - Eur(10.01m)).ShouldBe(Eur(-0.01m));
        Eur(1m).Add(Eur(2m)).ShouldBe(Eur(3m));
        Eur(1m).Subtract(Eur(2m)).ShouldBe(Eur(-1m));
    }

    [Fact]
    public void Negation_flips_the_sign()
    {
        (-Eur(5m)).ShouldBe(Eur(-5m));
        Eur(-5m).Negate().ShouldBe(Eur(5m));
        Eur(-5m).IsNegative.ShouldBeTrue();
    }

    [Fact]
    public void Multiplication_is_not_rounded()
    {
        (Eur(10.00m) * 0.035m).ShouldBe(Eur(0.35m));
        Eur(100.00m).Multiply(0.0001234m).Amount.ShouldBe(0.01234m);
    }

    [Fact]
    public void Comparison_follows_the_amount()
    {
        (Eur(1m) < Eur(2m)).ShouldBeTrue();
        (Eur(2m) <= Eur(2.00m)).ShouldBeTrue();
        (Eur(3m) > Eur(-3m)).ShouldBeTrue();
        (Eur(3m) >= Eur(4m)).ShouldBeFalse();
        new[] { Eur(3m), Eur(-1m), Eur(2m) }.Order().ShouldBe([Eur(-1m), Eur(2m), Eur(3m)]);
    }

    [Fact]
    public void Operators_with_different_currencies_throw()
    {
        var usd = Money.Of(1m, _usd);

        Should.Throw<InvalidOperationException>(() => Eur(1m) + usd).Message.ShouldContain("EUR and USD");
        Should.Throw<InvalidOperationException>(() => Eur(1m) - usd);
        Should.Throw<InvalidOperationException>(() => Eur(1m) < usd);
        Should.Throw<InvalidOperationException>(() => Eur(1m).CompareTo(usd));
    }

    [Fact]
    public void TryAdd_and_TrySubtract_return_the_result_for_the_same_currency()
    {
        Eur(1m).TryAdd(Eur(2m)).Value.ShouldBe(Eur(3m));
        Eur(1m).TrySubtract(Eur(2m)).Value.ShouldBe(Eur(-1m));
    }

    [Fact]
    public void TryAdd_and_TrySubtract_return_a_currency_mismatch_for_different_currencies()
    {
        var usd = Money.Of(1m, _usd);

        foreach (var result in new[] { Eur(1m).TryAdd(usd), Eur(1m).TrySubtract(usd) })
        {
            result.IsFailure.ShouldBeTrue();
            result.Error.Kind.ShouldBe(ErrorKind.Validation);
            result.Error.Code.ShouldBe("shared.currency-mismatch");
        }
    }

    [Fact]
    public void Overflow_throws() =>
        Should.Throw<OverflowException>(() => Eur(decimal.MaxValue) + Eur(1m));

    [Fact]
    public void The_default_money_is_not_money()
    {
        Should.Throw<InvalidOperationException>(() => default(Money) + Eur(1m));
        Should.Throw<InvalidOperationException>(() => Eur(1m) + default(Money));
        Should.Throw<InvalidOperationException>(() => default(Money).TryAdd(Eur(1m)));
        Should.Throw<InvalidOperationException>(() => default(Money).Round());
        Should.Throw<InvalidOperationException>(() => -default(Money));
        Should.Throw<InvalidOperationException>(() => default(Money).IsZero);
    }
}
