namespace RimBank.SharedKernel.Testing;

/// <summary>A <see cref="TimeProvider"/> whose clock only moves when a test moves it.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward by <paramref name="delta"/>.</summary>
    public void Advance(TimeSpan delta) => _now += delta;
}
