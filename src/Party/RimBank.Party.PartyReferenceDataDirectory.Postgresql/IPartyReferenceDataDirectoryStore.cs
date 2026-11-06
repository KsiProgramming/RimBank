using Marten;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql;

/// <summary>
/// The Party Reference Data Directory's own Marten store: schema <c>party_reference_data_directory</c>, role
/// <c>rimbank_party_reference_data_directory</c> and its own async daemon (ADR 0016).
/// </summary>
public interface IPartyReferenceDataDirectoryStore : IDocumentStore;
