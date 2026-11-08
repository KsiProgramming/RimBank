namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A change of one Reference value in a Reference Update (roadmap 1.18): set it to <see cref="Value"/>, or clear it when
/// <see cref="Value"/> is <c>null</c>. A <c>null</c> change (no instance) keeps the current value.
/// </summary>
/// <typeparam name="TValue">The value type, nullable so that a change can clear it (for example <c>CountryCode?</c>).</typeparam>
/// <param name="Value">The new value, or <c>null</c> to clear it.</param>
public sealed record PartyReferenceChange<TValue>(TValue Value);

/// <summary>Applies an optional <see cref="PartyReferenceChange{TValue}"/> to a current value.</summary>
internal static class PartyReferenceChangeExtensions
{
    /// <summary>The value after the change: the current one when there is no change.</summary>
    public static TValue ApplyTo<TValue>(this PartyReferenceChange<TValue>? change, TValue current) =>
        change switch
        {
            null => current,
            _ => change.Value,
        };
}
