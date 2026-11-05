using System.Text.Json;
using FsCheck.Xunit;
using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.MoneyAndCurrency.Tests;

[Properties(Arbitrary = [typeof(MoneyArbitraries)])]
public sealed class MoneyTextProperties
{
    [Property]
    public bool A_formatted_rounded_amount_parses_back(RoundedMoney rounded) =>
        Money.TryCreate(rounded.Value.FormatAmount(), rounded.Value.Currency.Code).Value == rounded.Value;

    [Property]
    public bool Json_round_trips_any_money(Money money) =>
        JsonSerializer.Deserialize<Money>(JsonSerializer.Serialize(money)) == money;

    [Property]
    public bool An_unrounded_amount_is_refused_as_client_input(Money money) =>
        money.IsRoundedToMinorUnits
        || Money.TryCreate(money.FormatAmount(), money.Currency.Code).Error.Code == Money.InvalidAmountCode;
}
