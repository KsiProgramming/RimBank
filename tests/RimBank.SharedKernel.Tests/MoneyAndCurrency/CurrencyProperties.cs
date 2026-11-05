using FsCheck.Xunit;
using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.Tests.MoneyAndCurrency;

[Properties(Arbitrary = [typeof(MoneyArbitraries)])]
public sealed class CurrencyProperties
{
    [Property]
    public bool Every_currency_is_found_by_its_code(Currency currency) =>
        Currency.TryCreate(currency.Code).Value == currency;

    [Property]
    public bool A_lower_case_code_is_never_a_currency(Currency currency) =>
        Currency.TryCreate(currency.Code.ToLowerInvariant()).IsFailure;
}
