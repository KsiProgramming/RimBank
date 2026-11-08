using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A party identifier held by the entry's Reference Behavior Qualifier (ADR 0029): a value object over its normalised
/// text, written to JSON as that text by <see cref="PartyIdentifierJsonConverter{TIdentifier}"/>.
/// </summary>
/// <typeparam name="TSelf">The identifier type.</typeparam>
public interface IPartyIdentifier<TSelf>
    where TSelf : class, IPartyIdentifier<TSelf>
{
    /// <summary>The normalised identifier, never blank.</summary>
    string Value { get; }

    /// <summary>
    /// Reads an identifier a caller sent, or one read back from storage: normalises it and checks its format.
    /// </summary>
    /// <param name="text">The text sent.</param>
    /// <returns>The identifier, or a validation error whose message says what is wrong.</returns>
    static abstract Result<TSelf> TryCreate(string? text);
}
