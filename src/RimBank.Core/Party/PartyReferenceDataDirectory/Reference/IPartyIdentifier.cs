namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A party identifier held by the entry's Reference Behavior Qualifier (ADR 0029): a text value object whose format is
/// that of an identifier (tax, national registration, LEI).
/// </summary>
/// <typeparam name="TSelf">The identifier type.</typeparam>
public interface IPartyIdentifier<TSelf> : IPartyReferenceText<TSelf>
    where TSelf : class, IPartyIdentifier<TSelf>;
