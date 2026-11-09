namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

/// <summary>A registered directory entry: its id, version and the ids of its Behavior Qualifiers.</summary>
public sealed record Entry(Guid Id, long Version, Guid DemographicsId, Guid ReferenceId, Guid BankRelationsId)
{
    /// <summary>Registers a party through its tool, with a fresh idempotency key.</summary>
    public static async Task<Entry> RegisterAsync(PartyMcpHost host, string partyType = "Person")
    {
        var entry = (await RegisterPartyToolTests.RegisterAsync(host, partyType, PartyTools.Key("register"))).Success();
        return new Entry(
            entry.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid(),
            entry.GetProperty("version").GetInt64(),
            entry.GetProperty(nameof(DemographicsId)).GetGuid(),
            entry.GetProperty(nameof(ReferenceId)).GetGuid(),
            entry.GetProperty(nameof(BankRelationsId)).GetGuid());
    }
}

/// <summary>Shared steps of the Party tool tests (roadmap 1.17).</summary>
public static class PartyTools
{
    private static int _keys;

    /// <summary>Registers a party through its tool, with a fresh idempotency key.</summary>
    /// <returns>The entry's id and version.</returns>
    public static async Task<(Guid Id, long Version)> RegisterAsync(PartyMcpHost host, string partyType)
    {
        var entry = (await RegisterPartyToolTests.RegisterAsync(host, partyType, Key("register"))).Success();
        return (entry.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid(), entry.GetProperty("version").GetInt64());
    }

    /// <summary>A fresh idempotency key starting with <paramref name="prefix"/>.</summary>
    public static string Key(string prefix) => $"{prefix}-{Interlocked.Increment(ref _keys)}";

    /// <summary>The number of events of the stream <paramref name="id"/>.</summary>
    public static string EventsOf(Guid id) =>
        $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{id}'";
}
