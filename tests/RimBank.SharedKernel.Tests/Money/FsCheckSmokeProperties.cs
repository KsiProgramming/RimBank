using FsCheck.Xunit;

namespace RimBank.SharedKernel.Tests.Money;

public sealed class FsCheckSmokeProperties
{
    [Property]
    public bool Decimal_addition_is_commutative(decimal a, decimal b) => a + b == b + a;
}
