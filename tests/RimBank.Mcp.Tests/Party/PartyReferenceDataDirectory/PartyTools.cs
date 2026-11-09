namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

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

    /// <summary>Registers a party and answers the whole Register result, for the ids of its Behavior Qualifiers.</summary>
    public static async Task<System.Text.Json.JsonElement> RegisterEntryAsync(PartyMcpHost host, string partyType) =>
        (await RegisterPartyToolTests.RegisterAsync(host, partyType, Key("register"))).Success();

    /// <summary>A fresh idempotency key starting with <paramref name="prefix"/>.</summary>
    public static string Key(string prefix) => $"{prefix}-{Interlocked.Increment(ref _keys)}";

    /// <summary>The number of events of the stream <paramref name="id"/>.</summary>
    public static string EventsOf(Guid id) =>
        $"SELECT count(*) FROM party_reference_data_directory.mt_events WHERE stream_id = '{id}'";
}
