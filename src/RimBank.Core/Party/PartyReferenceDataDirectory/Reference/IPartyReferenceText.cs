using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A text value held by the entry's Reference Behavior Qualifier (ADR 0029), such as an identifier or a name: a value
/// object over its normalised text, written to JSON as that text by <see cref="PartyReferenceTextJsonConverter{TText}"/>.
/// </summary>
/// <typeparam name="TSelf">The value type.</typeparam>
public interface IPartyReferenceText<TSelf>
    where TSelf : class, IPartyReferenceText<TSelf>
{
    /// <summary>The normalised text, never blank.</summary>
    string Value { get; }

    /// <summary>
    /// Reads a value a caller sent, or one read back from storage: normalises it and checks its format.
    /// </summary>
    /// <param name="text">The text sent.</param>
    /// <returns>The value, or a validation error whose message says what is wrong.</returns>
    static abstract Result<TSelf> TryCreate(string? text);
}
