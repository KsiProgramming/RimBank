using RimBank.SharedKernel;

namespace RimBank.Postgresql.Tests;

/// <summary>Fresh idempotency keys for the fake contexts' commands, as a caller sends a new UUID per request.</summary>
public static class Keys
{
    public static IdempotencyKey New() => IdempotencyKey.From(Guid.CreateVersion7().ToString());
}
