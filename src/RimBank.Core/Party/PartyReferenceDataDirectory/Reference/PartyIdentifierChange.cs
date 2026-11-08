namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A change of one party identifier in a Reference Update: set it to <see cref="Value"/>, or clear it when
/// <see cref="Value"/> is <c>null</c>. A <c>null</c> change (no instance) keeps the current identifier.
/// </summary>
/// <typeparam name="TIdentifier">The identifier type.</typeparam>
/// <param name="Value">The new identifier, or <c>null</c> to clear it.</param>
public sealed record PartyIdentifierChange<TIdentifier>(TIdentifier? Value)
    where TIdentifier : class, IPartyIdentifier<TIdentifier>;
